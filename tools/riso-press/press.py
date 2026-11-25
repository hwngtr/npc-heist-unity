"""
Riso Press: 2D isometric risograph print engine on Cairo.

Renders warm cream paper (#F3EBDD) with 5 named riso inks overprinted through
a shared 8x8 ordered Bayer dither matrix, deterministic hand-wobbled 1px
midnight-blue outlines, paper knockouts, dither gradients, and soft halos.

Design principles follow the riso-interface-craft system:
- Paper-first light ink tints (0.12-0.45) rather than heavy solid RGB blocks.
- Shared 8x8 Bayer screen across every ink so overprints land on matching dots.
- Deterministic seeded PRNG per entity so previews and exports never flicker.
"""

from __future__ import annotations

import math
from typing import Callable, Dict, Iterable, List, Optional, Sequence, Tuple

import cairo

TAU = math.tau

# Isometric projection: 64x32 diamond tiles, 32 px per unit of height.
TW = 64.0
TH = 32.0
TZ = 32.0


def iso_x(i: float, j: float) -> float:
    return (i - j) * (TW * 0.5)


def iso_y(i: float, j: float, z: float = 0.0) -> float:
    return (i + j) * (TH * 0.5) - z * TZ


def iso(i: float, j: float, z: float = 0.0) -> Tuple[float, float]:
    return (iso_x(i, j), iso_y(i, j, z))


# Named riso ink drums (RGB 0..255).
PAPER: Tuple[int, int, int] = (243, 235, 221)
INK: Dict[str, Tuple[int, int, int]] = {
    "blue": (52, 74, 128),      # key plate: midnight blue
    "coral": (238, 104, 82),    # warm clay
    "sun": (255, 212, 40),      # yellow
    "teal": (0, 140, 138),      # green-blue
    "black": (36, 34, 38),      # deep shadow / accents
    "paper": PAPER,
}

# Subtle fixed plate misregistration per drum (in world px).
PLATE: Dict[str, Tuple[float, float]] = {
    "blue": (0.0, 0.0),
    "coral": (-0.45, 0.30),
    "sun": (0.35, 0.50),
    "teal": (-0.30, -0.40),
    "black": (0.0, 0.0),
    "paper": (0.0, 0.0),
}

SHIFT: Dict[str, Tuple[int, int]] = {
    "blue": (0, 0),
    "coral": (0, 0),
    "sun": (0, 0),
    "teal": (0, 0),
    "black": (0, 0),
    "paper": (4, 4),
}


def _build_bayer8() -> List[List[int]]:
    base = [[0, 2], [3, 1]]
    mat = base
    n = 2
    while n < 8:
        nxt = []
        for y in range(2 * n):
            row = []
            for x in range(2 * n):
                row.append(4 * mat[y % n][x % n] + base[y // n][x // n])
            nxt.append(row)
        mat = nxt
        n *= 2
    return mat


BAYER8: List[List[int]] = _build_bayer8()
LEVELS = 64


# ---------------- Deterministic PRNG (Mulberry32 + FNV-1a) ----------------

def _u32(v: int) -> int:
    return v & 0xFFFFFFFF


def _imul(a: int, b: int) -> int:
    return _u32(a * b)


def str_hash(s: str) -> int:
    h = 0x811C9DC5
    for ch in s:
        h ^= ord(ch)
        h = _imul(h, 16777619)
    return _u32(h)


def hmix(h: int, v: int) -> int:
    h = _imul(h ^ _u32(v), 0x85EBCA6B)
    h ^= h >> 13
    h = _imul(h, 0xC2B2AE35)
    return _u32(h ^ (h >> 16))


class SeededRng:
    """Deterministic Mulberry32 generator."""

    def __init__(self, seed: int) -> None:
        self._a = _u32(seed)

    def __call__(self) -> float:
        self._a = _u32(self._a + 0x6D2B79F5)
        a = self._a
        t = _imul(a ^ (a >> 15), 1 | a)
        t = _u32(t + _imul(t ^ (t >> 7), 61 | t)) ^ t
        return _u32(t ^ (t >> 14)) / 4294967296.0


def stream_of(*keys: object) -> SeededRng:
    h = 0x9E3779B9
    for k in keys:
        if isinstance(k, str):
            val = str_hash(k)
        elif isinstance(k, float):
            val = int(round(k * 1000))
        else:
            val = int(k)
        h = hmix(h, val)
    rng = SeededRng(h)
    for _ in range(11):
        rng()
    return rng


def rd(rng: SeededRng, a: float, b: float) -> float:
    return a + rng() * (b - a)


def ri(rng: SeededRng, a: int, b: int) -> int:
    return int(math.floor(rd(rng, float(a), float(b + 1))))


def pk(rng: SeededRng, seq: Sequence):
    return seq[int(rng() * len(seq)) % len(seq)]


def clamp(v: float, lo: float, hi: float) -> float:
    return lo if v < lo else (hi if v > hi else v)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def lerp_p(a: Sequence[float], b: Sequence[float], t: float) -> Tuple[float, float]:
    return (lerp(a[0], b[0], t), lerp(a[1], b[1], t))


# ---------------- 2D Geometry helpers ----------------

Point = Tuple[float, float]


def resample(pts: Sequence[Point], step: float) -> List[Point]:
    if len(pts) < 2:
        return [(p[0], p[1]) for p in pts]
    out: List[Point] = [(pts[0][0], pts[0][1])]
    need = step
    for i in range(1, len(pts)):
        x0, y0 = pts[i - 1]
        x1, y1 = pts[i]
        d = math.hypot(x1 - x0, y1 - y0)
        while d >= need and d > 1e-9:
            t = need / d
            x0 += (x1 - x0) * t
            y0 += (y1 - y0) * t
            out.append((x0, y0))
            d -= need
            need = step
        need -= d
    lx, ly = pts[-1]
    ex, ey = out[-1]
    if math.hypot(lx - ex, ly - ey) > step * 0.3:
        out.append((lx, ly))
    return out


def chaikin(pts: Sequence[Point], closed: bool, iterations: int) -> List[Point]:
    p = [(pt[0], pt[1]) for pt in pts]
    for _ in range(iterations):
        n = len(p)
        if n < 2:
            break
        q: List[Point] = []
        if closed:
            for i in range(n):
                a = p[i]
                b = p[(i + 1) % n]
                q.append((a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25))
                q.append((a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75))
        else:
            q.append(p[0])
            for i in range(n - 1):
                a = p[i]
                b = p[i + 1]
                q.append((a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25))
                q.append((a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75))
            q.append(p[-1])
        p = q
    return p


def curve(pts: Sequence[Point], it: int = 3) -> List[Point]:
    return chaikin(pts, False, it)


def loop(pts: Sequence[Point], it: int = 3) -> List[Point]:
    return chaikin(pts, True, it)


def bbox(pts: Sequence[Point]) -> Tuple[float, float, float, float]:
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return (min(xs), min(ys), max(xs), max(ys))


def centroid(pts: Sequence[Point]) -> Point:
    n = max(1, len(pts))
    return (sum(p[0] for p in pts) / n, sum(p[1] for p in pts) / n)


def pip(poly: Sequence[Point], x: float, y: float) -> bool:
    inside = False
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / (yj - yi) + xi):
            inside = not inside
        j = i
    return inside


def arc_pts(
    cx: float, cy: float, rx: float, ry: float, a0: float, a1: float, n: int
) -> List[Point]:
    n = max(2, n)
    return [
        (cx + math.cos(a0 + (a1 - a0) * k / n) * rx, cy + math.sin(a0 + (a1 - a0) * k / n) * ry)
        for k in range(n + 1)
    ]


def ell(cx: float, cy: float, rx: float, ry: float, n: Optional[int] = None) -> List[Point]:
    n = n or max(14, int(round((rx + ry) * 0.6)))
    return arc_pts(cx, cy, rx, ry, 0.0, TAU, n)[:-1]


def circ(cx: float, cy: float, r: float, n: Optional[int] = None) -> List[Point]:
    return ell(cx, cy, r, r, n)


def star_pts(
    cx: float, cy: float, r1: float, r2: float, n: int, rot: float = 0.0
) -> List[Point]:
    out: List[Point] = []
    for i in range(n * 2):
        a = rot + i / (n * 2) * TAU
        r = r2 if (i % 2) else r1
        out.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return out


def blob(
    rng: SeededRng, cx: float, cy: float, rx: float, ry: float, rot0: Optional[float] = None
) -> List[Point]:
    n = max(12, int(round((rx + ry) / 3.0)))
    rot = rd(rng, 0.0, TAU) if rot0 is None else rot0
    a1 = rd(rng, 0.04, 0.10)
    a2 = rd(rng, 0.02, 0.05)
    p1 = rd(rng, 0.0, 7.0)
    p2 = rd(rng, 0.0, 7.0)
    cr, sr = math.cos(rot), math.sin(rot)
    pts: List[Point] = []
    for i in range(n):
        t = i / n * TAU
        m = 1.0 + a1 * math.sin(t * 2.0 + p1) + a2 * math.sin(t * 5.0 + p2)
        x = math.cos(t) * rx * m
        y = math.sin(t) * ry * m
        pts.append((cx + x * cr - y * sr, cy + x * sr + y * cr))
    return pts


def ribbon(pts: Sequence[Point], width) -> List[Point]:
    n = len(pts)
    left: List[Point] = []
    right: List[Point] = []
    for i in range(n):
        a = pts[max(0, i - 1)]
        b = pts[min(n - 1, i + 1)]
        nx = -(b[1] - a[1])
        ny = b[0] - a[0]
        d = math.hypot(nx, ny) or 1.0
        nx /= d
        ny /= d
        t = i / (n - 1) if n > 1 else 0.0
        ww = (width(t) if callable(width) else float(width)) * 0.5
        left.append((pts[i][0] + nx * ww, pts[i][1] + ny * ww))
        right.append((pts[i][0] - nx * ww, pts[i][1] - ny * ww))
    right.reverse()
    return left + right


# ---------------- Bayer 8x8 Pattern Bank ----------------

_TILE_CACHE: Dict[Tuple[str, int, int], cairo.ImageSurface] = {}


def dither_tile_surface(ink: str, level: int, alpha_255: int = 255) -> cairo.ImageSurface:
    key = (ink, level, alpha_255)
    surf = _TILE_CACHE.get(key)
    if surf is not None:
        return surf

    surf = cairo.ImageSurface(cairo.FORMAT_ARGB32, 8, 8)
    buf = surf.get_data()
    stride = surf.get_stride()
    r, g, b = INK.get(ink, INK["blue"])
    sx, sy = SHIFT.get(ink, (0, 0))
    # Cairo FORMAT_ARGB32 on little-endian stores premultiplied BGRA bytes.
    a = alpha_255
    pr = (r * a + 127) // 255
    pg = (g * a + 127) // 255
    pb = (b * a + 127) // 255

    for y in range(8):
        row_off = y * stride
        for x in range(8):
            if BAYER8[(y + sy) & 7][(x + sx) & 7] < level:
                o = row_off + x * 4
                buf[o + 0] = pb
                buf[o + 1] = pg
                buf[o + 2] = pr
                buf[o + 3] = a
    surf.mark_dirty()
    _TILE_CACHE[key] = surf
    return surf


def make_ink_pattern(
    ink: str, tone: float, fine: bool = False, dp: float = 1.0, alpha_255: int = 255
) -> cairo.SurfacePattern:
    level = int(clamp(round(tone * LEVELS), 1, LEVELS))
    surf = dither_tile_surface(ink, level, alpha_255)
    pat = cairo.SurfacePattern(surf)
    pat.set_extend(cairo.EXTEND_REPEAT)
    pat.set_filter(cairo.FILTER_NEAREST)
    scale = (0.5 if fine else 1.0) * dp
    if abs(scale - 1.0) > 1e-6:
        mat = cairo.Matrix()
        mat.scale(1.0 / scale, 1.0 / scale)
        pat.set_matrix(mat)
    return pat


# ---------------- The Riso Hand ----------------

class RisoHand:
    """High-level riso drawing context wrapping a Cairo Context."""

    def __init__(
        self,
        ctx: cairo.Context,
        ox: float = 0.0,
        oy: float = 0.0,
        seed: object = 0,
        fine: bool = False,
        dp: float = 1.0,
        transparent_sprite: bool = False,
    ) -> None:
        self.ctx = ctx
        self.ox = ox
        self.oy = oy
        self.fine = fine
        self.dp = dp
        self.transparent_sprite = transparent_sprite
        self.rb = stream_of(seed, "hand")

    # Isometric projection helpers
    def p(self, i: float, j: float, z: float = 0.0) -> Point:
        return (iso_x(i, j) + self.ox, iso_y(i, j, z) + self.oy)

    def tile(self, i: float, j: float, w: float, d: float, z: float = 0.0) -> List[Point]:
        return [
            self.p(i, j, z),
            self.p(i + w, j, z),
            self.p(i + w, j + d, z),
            self.p(i, j + d, z),
        ]

    def face_i(self, i: float, j: float, length: float, z0: float, z1: float) -> List[Point]:
        return [
            self.p(i, j, z0),
            self.p(i + length, j, z0),
            self.p(i + length, j, z1),
            self.p(i, j, z1),
        ]

    def face_j(self, i: float, j: float, length: float, z0: float, z1: float) -> List[Point]:
        return [
            self.p(i, j, z0),
            self.p(i, j + length, z0),
            self.p(i, j + length, z1),
            self.p(i, j, z1),
        ]

    def _path(self, pts: Sequence[Point], dx: float = 0.0, dy: float = 0.0) -> None:
        c = self.ctx
        c.new_path()
        for idx, pt in enumerate(pts):
            if idx == 0:
                c.move_to(pt[0] + dx, pt[1] + dy)
            else:
                c.line_to(pt[0] + dx, pt[1] + dy)
        c.close_path()

    def _print_ink(
        self,
        ink: str,
        tone: float,
        draw_fn: Callable[[cairo.SurfacePattern, float, float], None],
        fine: Optional[bool] = None,
        over_transparent: bool = False,
    ) -> None:
        k = ink if ink in INK else "blue"
        dx, dy = PLATE[k]
        use_fine = self.fine if fine is None else fine
        c = self.ctx
        c.save()
        if over_transparent:
            # Semi-transparent Bayer ink dots so cast shadows blend naturally onto any Unity floor.
            c.set_operator(cairo.OPERATOR_OVER)
            pat = make_ink_pattern(k, tone, use_fine, self.dp, alpha_255=148)
        elif k != "paper":
            c.set_operator(cairo.OPERATOR_MULTIPLY)
            pat = make_ink_pattern(k, tone, use_fine, self.dp, alpha_255=255)
        else:
            c.set_operator(cairo.OPERATOR_OVER)
            pat = make_ink_pattern(k, tone, use_fine, self.dp, alpha_255=255)
        draw_fn(pat, dx, dy)
        c.restore()

    def knock(self, pts: Sequence[Point], alpha: float = 1.0) -> None:
        if len(pts) < 3:
            return
        c = self.ctx
        c.save()
        c.set_operator(cairo.OPERATOR_OVER)
        self._path(pts)
        c.set_source_rgba(PAPER[0] / 255.0, PAPER[1] / 255.0, PAPER[2] / 255.0, alpha)
        c.fill()
        c.restore()

    def tint(
        self,
        pts: Sequence[Point],
        ink: str,
        tone: float = 1.0,
        fine: Optional[bool] = None,
        over_transparent: bool = False,
    ) -> None:
        if len(pts) < 3 or tone <= 0.0:
            return

        def _draw(pat: cairo.SurfacePattern, dx: float, dy: float) -> None:
            self._path(pts, dx, dy)
            self.ctx.set_source(pat)
            self.ctx.fill()

        self._print_ink(ink, tone, _draw, fine=fine, over_transparent=over_transparent)

    def fill(
        self,
        pts: Sequence[Point],
        ink: str,
        tone: float = 1.0,
        fine: Optional[bool] = None,
        knock: bool = True,
    ) -> None:
        if len(pts) < 3:
            return
        if knock:
            self.knock(pts)
        if tone > 0.0 and ink != "paper":
            self.tint(pts, ink, tone, fine=fine)

    def fill2(
        self,
        pts: Sequence[Point],
        ink: str,
        tone: float,
        ink2: Optional[str],
        tone2: float = 0.0,
        fine: Optional[bool] = None,
    ) -> None:
        self.fill(pts, ink, tone, fine=fine)
        if ink2 and tone2 > 0.0:
            self.tint(pts, ink2, tone2, fine=fine)

    def clip(self, pts: Sequence[Point], fn: Callable[[], None]) -> None:
        if len(pts) < 3:
            return
        c = self.ctx
        c.save()
        self._path(pts)
        c.clip()
        try:
            fn()
        finally:
            c.restore()

    def grad(
        self,
        pts: Sequence[Point],
        ink: str,
        t0: float,
        t1: float,
        a: Point,
        b: Point,
        n: int = 12,
        fine: Optional[bool] = None,
    ) -> None:
        if len(pts) < 3:
            return
        dx = b[0] - a[0]
        dy = b[1] - a[1]
        length = math.hypot(dx, dy) or 1.0
        ux, uy = dx / length, dy / length
        nx, ny = -uy, ux
        w = 4000.0

        def _inner() -> None:
            for k in range(n):
                s0 = k / n * length - (0.0 if k else w)
                s1 = (k + 1) / n * length + (w if k == n - 1 else 0.0)
                t = lerp(t0, t1, (k + 0.5) / n)
                if t <= 0.02:
                    continue
                band = [
                    (a[0] + ux * s0 + nx * w, a[1] + uy * s0 + ny * w),
                    (a[0] + ux * (s1 + 0.5) + nx * w, a[1] + uy * (s1 + 0.5) + ny * w),
                    (a[0] + ux * (s1 + 0.5) - nx * w, a[1] + uy * (s1 + 0.5) - ny * w),
                    (a[0] + ux * s0 - nx * w, a[1] + uy * s0 - ny * w),
                ]
                self.tint(band, ink, t, fine=fine)

        self.clip(pts, _inner)

    def _rings(
        self,
        ink: str,
        x: float,
        y: float,
        rx: float,
        ry: float,
        n: int,
        tone_at: Callable[[float, float], float],
        over_transparent: bool = False,
    ) -> None:
        for k in range(n):
            f0 = k / n
            f1 = (k + 1) / n
            t = clamp(tone_at(f0, f1), 0.0, 1.0)
            if t <= 0.03:
                continue

            def _draw(pat: cairo.SurfacePattern, dx: float, dy: float) -> None:
                c = self.ctx
                c.save()
                c.set_fill_rule(cairo.FILL_RULE_EVEN_ODD)
                c.new_path()
                c.save()
                c.translate(x + dx, y + dy)
                c.scale(max(0.1, rx * f1), max(0.1, ry * f1))
                c.arc(0.0, 0.0, 1.0, 0.0, TAU)
                c.restore()
                if k > 0:
                    c.save()
                    c.translate(x + dx, y + dy)
                    c.scale(max(0.1, rx * f0), max(0.1, ry * f0))
                    c.new_sub_path()
                    c.arc(0.0, 0.0, 1.0, 0.0, TAU)
                    c.restore()
                c.set_source(pat)
                c.fill()
                c.restore()

            self._print_ink(ink, t, _draw, over_transparent=over_transparent)

    def glow(
        self,
        x: float,
        y: float,
        rx: float,
        ry: float,
        ink: str = "sun",
        tone: float = 0.55,
        n: int = 8,
    ) -> None:
        if tone <= 0.02 or rx <= 0 or ry <= 0:
            return
        self._rings(
            ink,
            x,
            y,
            rx,
            ry,
            n,
            lambda f0, f1: tone * math.pow(max(0.0, 1.0 - (f0 + f1) * 0.5), 1.3),
            over_transparent=self.transparent_sprite,
        )

    def wob(self, rs: Sequence[Point], amp: float) -> List[Point]:
        n = len(rs)
        p1 = rd(self.rb, 0.0, 7.0)
        p2 = rd(self.rb, 0.0, 7.0)
        f1 = rd(self.rb, 1.4, 3.0)
        f2 = rd(self.rb, 5.0, 9.0)
        out: List[Point] = []
        for i in range(n):
            t = i / (n - 1) if n > 1 else 0.0
            a = rs[i - 1] if i > 0 else rs[0]
            b = rs[i + 1] if i < n - 1 else rs[-1]
            nx = -(b[1] - a[1])
            ny = b[0] - a[0]
            d = math.hypot(nx, ny) or 1.0
            nx /= d
            ny /= d
            off = amp * (0.65 * math.sin(t * f1 * 2.0 + p1) + 0.35 * math.sin(t * f2 + p2))
            out.append(
                (
                    rs[i][0] + nx * off + rd(self.rb, -0.1, 0.1),
                    rs[i][1] + ny * off + rd(self.rb, -0.1, 0.1),
                )
            )
        return out

    def line(
        self,
        rng: SeededRng,
        pts: Sequence[Point],
        ink: str = "blue",
        w: float = 1.4,
        tone: float = 1.0,
        amp: Optional[float] = None,
        dash: Optional[Sequence[float]] = None,
        closed: bool = False,
        fine: Optional[bool] = None,
        cap: str = "round",
    ) -> None:
        if not pts or len(pts) < 2:
            return
        src = list(pts) + [pts[0]] if closed else list(pts)
        rs = resample(src, max(2.5, w * 1.4))
        if len(rs) < 2:
            return
        use_amp = min(0.6, w * 0.15 + 0.15) if amp is None else amp
        q = self.wob(rs, use_amp) if use_amp > 0 else rs
        eff_tone = 1.0 if tone >= 0.75 else tone

        def _draw(pat: cairo.SurfacePattern, dx: float, dy: float) -> None:
            c = self.ctx
            c.set_source(pat)
            c.set_line_width(w)
            c.set_line_cap(cairo.LINE_CAP_BUTT if cap == "butt" else cairo.LINE_CAP_ROUND)
            c.set_line_join(cairo.LINE_JOIN_ROUND)
            if dash:
                c.set_dash(list(dash))
            else:
                c.set_dash([])
            c.new_path()
            c.move_to(q[0][0] + dx, q[0][1] + dy)
            for pt in q[1:]:
                c.line_to(pt[0] + dx, pt[1] + dy)
            c.stroke()
            c.set_dash([])

        # Outlines over transparent sprite edges must stay crisp opaque ink (OPERATOR_OVER).
        if self.transparent_sprite and eff_tone >= 0.99 and ink != "paper":
            k = ink if ink in INK else "blue"
            dx, dy = PLATE[k]
            r, g, b = INK[k]
            c = self.ctx
            c.save()
            c.set_operator(cairo.OPERATOR_OVER)
            c.set_source_rgba(r / 255.0, g / 255.0, b / 255.0, 1.0)
            c.set_line_width(w)
            c.set_line_cap(cairo.LINE_CAP_BUTT if cap == "butt" else cairo.LINE_CAP_ROUND)
            c.set_line_join(cairo.LINE_JOIN_ROUND)
            if dash:
                c.set_dash(list(dash))
            c.new_path()
            c.move_to(q[0][0] + dx, q[0][1] + dy)
            for pt in q[1:]:
                c.line_to(pt[0] + dx, pt[1] + dy)
            c.stroke()
            c.restore()
            return

        self._print_ink(ink, eff_tone, _draw, fine=fine)

    def outline(
        self,
        rng: SeededRng,
        pts: Sequence[Point],
        ink: str = "blue",
        w: float = 1.2,
        tone: float = 0.9,
        amp: float = 0.15,
        fine: Optional[bool] = None,
    ) -> None:
        self.line(rng, pts, ink=ink, w=w, tone=tone, amp=amp, closed=True, fine=fine)

    def dot(
        self,
        x: float,
        y: float,
        r: float,
        ink: str = "blue",
        tone: float = 1.0,
        knock: bool = False,
        fine: Optional[bool] = None,
    ) -> None:
        pts = circ(x, y, r, max(8, int(round(r * 2.5))))
        if knock:
            self.fill(pts, ink, tone, fine=fine)
        else:
            self.tint(pts, ink, tone, fine=fine)

    def speckle(
        self,
        rng: SeededRng,
        pts: Sequence[Point],
        ink: str,
        count: int,
        r0: float = 0.6,
        r1: float = 1.4,
        tone: float = 1.0,
    ) -> None:
        x0, y0, x1, y1 = bbox(pts)
        dots: List[Tuple[float, float, float]] = []
        for _ in range(count):
            x = rd(rng, x0, x1)
            y = rd(rng, y0, y1)
            if pip(pts, x, y):
                dots.append((x, y, rd(rng, r0, r1)))

        def _draw(pat: cairo.SurfacePattern, dx: float, dy: float) -> None:
            c = self.ctx
            c.set_source(pat)
            c.new_path()
            for x, y, r in dots:
                c.new_sub_path()
                c.arc(x + dx, y + dy, r, 0.0, TAU)
            c.fill()

        self._print_ink(ink, tone, _draw)


def make_surface(width: int, height: int, fill_paper: bool = False) -> Tuple[cairo.ImageSurface, cairo.Context]:
    surf = cairo.ImageSurface(cairo.FORMAT_ARGB32, width, height)
    ctx = cairo.Context(surf)
    ctx.set_antialias(cairo.ANTIALIAS_DEFAULT)
    if fill_paper:
        ctx.set_source_rgb(PAPER[0] / 255.0, PAPER[1] / 255.0, PAPER[2] / 255.0)
        ctx.paint()
    return surf, ctx
