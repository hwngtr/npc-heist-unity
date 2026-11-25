"""
Declarative 2D isometric object renderer for the Riso Press engine.

Renders any object defined as a list of local (u, v, z) primitives:
box (with optional soft:true), legs, cyl, cone (with optional open:true),
ball, pole, panel (with frame, inset, round, shine, drop, art), plant,
books, flat (with stripes/border/dots/check/fringe/round/water), glow,
lines (with optional dash), and dot.
"""

from __future__ import annotations

import math
from typing import Any, Callable, Dict, List, Optional, Sequence, Tuple

from press import (
    Point,
    RisoHand,
    SeededRng,
    TAU,
    arc_pts,
    bbox,
    blob,
    circ,
    clamp,
    curve,
    ell,
    iso,
    lerp,
    lerp_p,
    loop,
    pk,
    rd,
    ri,
    ribbon,
    star_pts,
    stream_of,
)
from world import box_at, shadow_at, shadow_box

# Named swatch palette: [ink, tone] or [ink, tone, ink2, tone2].
# Every swatch is calibrated so warm paper breathes through and tones match bot-house-main.
SWATCH: Dict[str, List[Any]] = {
    "white": ["paper", 1.0],
    "cream": ["sun", 0.14],
    "beige": ["sun", 0.26, "coral", 0.10],
    "tan": ["sun", 0.42, "coral", 0.22],
    "oak": ["sun", 0.55, "coral", 0.22],
    "walnut": ["coral", 0.60, "blue", 0.42],
    "brown": ["coral", 0.70, "blue", 0.55],
    "terracotta": ["coral", 0.82, "sun", 0.35],
    "yellow": ["sun", 0.90],
    "sun": ["sun", 0.90],
    "mustard": ["sun", 0.85, "coral", 0.30],
    "orange": ["sun", 0.85, "coral", 0.65],
    "coral": ["coral", 0.75],
    "red": ["coral", 1.0, "blue", 0.15],
    "pink": ["coral", 0.35],
    "blush": ["coral", 0.20],
    "plum": ["coral", 0.65, "blue", 0.60],
    "purple": ["blue", 0.55, "coral", 0.50],
    "navy": ["blue", 0.92],
    "blue": ["blue", 0.62],
    "sky": ["blue", 0.24, "teal", 0.12],
    "aqua": ["teal", 0.50, "blue", 0.22],
    "teal": ["teal", 0.72],
    "green": ["teal", 0.70, "sun", 0.55],
    "leaf": ["teal", 0.78, "sun", 0.35],
    "sage": ["teal", 0.38, "sun", 0.25],
    "mint": ["teal", 0.30],
    "olive": ["sun", 0.65, "blue", 0.45],
    "silver": ["blue", 0.16],
    "grey": ["blue", 0.32],
    "charcoal": ["black", 0.72],
    "black": ["black", 0.95],
    "brass": ["sun", 0.80, "coral", 0.25],
    "glass": ["teal", 0.14, "blue", 0.10],
}

SHAPES = (
    "box",
    "legs",
    "cyl",
    "cone",
    "ball",
    "pole",
    "panel",
    "plant",
    "books",
    "flat",
    "glow",
    "lines",
    "dot",
)

R2 = math.sqrt(2.0)
RX = 32.0 * R2
RY = 16.0 * R2


def shape_type(s: Dict[str, Any]) -> Optional[str]:
    for key in SHAPES:
        if isinstance(s.get(key), list):
            return key
    return None


class Placer:
    """Transforms an object's local (u, v, z) space into room tile space (i, j, z)."""

    def __init__(self, at: Dict[str, Any], size: Sequence[float]) -> None:
        face = at.get("face", "ne")
        self.nw = face in ("nw", "se")
        self.flip = face in ("sw", "se")
        self.at_i = float(at.get("i", 0.0))
        self.at_j = float(at.get("j", 0.0))
        self.z0 = float(at.get("z", 0.0))
        self.sw = float(size[0])
        self.sd = float(size[1])

    def w(self, u: float, v: float, z: float = 0.0) -> Tuple[float, float, float]:
        if self.flip:
            u = self.sw - u
            v = self.sd - v
        if self.nw:
            return (self.at_i + v, self.at_j + u, self.z0 + z)
        return (self.at_i + u, self.at_j + v, self.z0 + z)

    def rect(self, u: float, v: float, w: float, d: float) -> Tuple[float, float, float, float]:
        a = self.w(u, v, 0.0)
        b = self.w(u + w, v + d, 0.0)
        return (min(a[0], b[0]), min(a[1], b[1]), abs(b[0] - a[0]), abs(b[1] - a[1]))

    def size(self, w: float, d: float) -> Tuple[float, float]:
        return (d, w) if self.nw else (w, d)


def _paint(h: RisoHand, pts: Sequence[Point], col: Sequence[Any], shade: float, fine: Optional[bool]) -> None:
    h.fill(pts, col[0], col[1], fine=fine)
    if len(col) > 2 and col[2]:
        h.tint(pts, col[2], col[3], fine=fine)
    if shade > 0.0:
        h.tint(pts, "blue", shade, fine=fine)


def _resolve_color(kind: Dict[str, Any], item: Dict[str, Any], name: Optional[str]) -> List[Any]:
    if not name:
        return SWATCH["grey"]
    item_cols = item.get("colors") or {}
    kind_cols = kind.get("colors") or {}
    resolved = item_cols.get(name) or kind_cols.get(name) or name
    return SWATCH.get(resolved, SWATCH["grey"])


def _resolve_shape(shape: Dict[str, Any], kind: Dict[str, Any], item: Dict[str, Any]) -> Dict[str, Any]:
    size = item.get("size") or kind["size"]
    opt = {**(kind.get("options") or {}), **(item.get("options") or {})}

    def val(x: Any) -> Any:
        if isinstance(x, str) and x.startswith("$"):
            if x == "$w":
                return size[0]
            if x in ("$h", "$d"):
                return size[1]
            return opt.get(x[1:], x)
        return x

    out: Dict[str, Any] = {}
    for k, v in shape.items():
        if isinstance(v, list) and k != "lines":
            out[k] = [val(e) for e in v]
        else:
            out[k] = val(v)
    return out


def _draw_cone(
    h: RisoHand,
    rng: SeededRng,
    ctx: Dict[str, Any],
    u: float,
    v: float,
    z: float,
    r0: float,
    r1: float,
    height: float,
    col: Sequence[Any],
    top_col: Optional[Sequence[Any]],
    s: Dict[str, Any],
) -> None:
    pl: Placer = ctx["pl"]
    fine = ctx["fine"]
    lw = ctx["lw"]
    cx, yb = h.p(*pl.w(u, v, z))
    yt = yb - height * 32.0
    a = r0 * RX
    b = r0 * RY
    a1 = r1 * RX
    b1 = r1 * RY
    if s.get("shadow") and height > 0.05:
        shadow_at(h, cx + 0.5, yb + 0.6, a * 1.06, b * 1.08, 0.34)
        shadow_at(h, cx + a * 0.30, yb + 1.6, a * 1.12, b * 1.06, 0.18)

    pts = arc_pts(cx, yb, a, b, 0.0, TAU, 36) + arc_pts(cx, yt, max(a1, 0.05), max(b1, 0.05), 0.0, TAU, 36)
    pts.sort(key=lambda pt: (pt[0], pt[1]))

    def cross(o: Point, p: Point, q: Point) -> float:
        return (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0])

    lo: List[Point] = []
    for pt in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], pt) <= 0:
            lo.pop()
        lo.append(pt)
    hi: List[Point] = []
    for pt in reversed(pts):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], pt) <= 0:
            hi.pop()
        hi.append(pt)
    side = lo[:-1] + hi[:-1]

    _paint(h, side, col, 0.0, fine)
    h.grad(side, "blue", 0.02, 0.42, (cx - max(a, a1), yb), (cx + max(a, a1), yb), 8, fine=fine)

    top_poly = ell(cx, yt, max(a1, 0.1), max(b1, 0.1), 24)
    if s.get("open"):
        _paint(h, top_poly, col, 0.45, fine)
    elif r1 > 0.01 and r1 >= r0 * 0.55:
        _paint(h, top_poly, top_col or col, 0.0, fine)
    if r1 < r0 * 0.55 and height < r0 * 0.9:
        h.line(rng, arc_pts(cx, yb, a, b, 0.15, math.pi - 0.15, 18), "blue", w=0.8, tone=0.7, amp=0.05)

    h.outline(rng, side, "blue", w=lw or 0.9, tone=0.9, amp=0.15, fine=fine)
    if r1 > 0.01 and (s.get("open") or r1 >= r0 * 0.55):
        h.outline(rng, top_poly, "blue", w=0.8, tone=0.9, amp=0.12, fine=fine)


def _draw_plant(h: RisoHand, rng: SeededRng, ctx: Dict[str, Any], s: Dict[str, Any]) -> None:
    pl: Placer = ctx["pl"]
    fine = ctx["fine"]
    u, v, z = s["plant"]
    k = float(s.get("size", 1.0))
    style = s.get("style", "leafy")
    col = ctx["col"](s.get("color", "leaf"))
    pr = stream_of("plant", ctx["item"].get("id", ""), u, v, z, style)
    x, y = h.p(*pl.w(u, v, z))

    def draw_leaf(poly: Sequence[Point], tone_mul: float) -> None:
        h.fill(poly, col[0], clamp(col[1] * tone_mul, 0.2, 1.0), fine=fine)
        if len(col) > 2 and col[2]:
            h.tint(poly, col[2], col[3], fine=fine)
        h.outline(rng, poly, "blue", w=0.7, tone=0.8, amp=0.15, fine=fine)

    def leaf_poly(x0: float, y0: float, x1: float, y1: float, w: float) -> List[Point]:
        mid = (lerp(x0, x1, 0.5) + rd(pr, -2.0, 2.0), lerp(y0, y1, 0.5) - 2.0)
        return ribbon(curve([(x0, y0), mid, (x1, y1)], 2), lambda t: w * math.sin(math.pi * min(1.0, t * 1.05 + 0.02)))

    if style == "leafy":
        for n in range(9):
            a = -math.pi * 0.5 + (n - 4) * 0.36 + rd(pr, -0.1, 0.1)
            length = rd(pr, 12.0, 20.0) * k
            draw_leaf(leaf_poly(x, y, x + math.cos(a) * length, y + math.sin(a) * length * 0.95, 6.0 * k), rd(pr, 0.8, 1.1))
    elif style == "flowers":
        pal = ["coral", "yellow", "pink", "white", "purple"]
        for _ in range(9):
            fx = x + rd(pr, -14.0, 14.0) * k
            fy = y + rd(pr, -4.0, 4.0) * k
            fh = rd(pr, 10.0, 18.0) * k
            h.line(rng, [(fx, fy), (fx + rd(pr, -1.5, 1.5), fy - fh)], "teal", w=1.0, tone=0.9, amp=0.05)
            draw_leaf(leaf_poly(fx, fy - 2.0, fx + rd(pr, -5.0, 5.0) * k, fy - 6.0 * k, 2.8 * k), 1.0)
            fc = SWATCH.get(pk(pr, pal), SWATCH["coral"])
            star = star_pts(fx, fy - fh, 4.2 * k, 2.2 * k, 5, rd(pr, 0.0, 1.0))
            h.fill(star, fc[0], max(0.8, fc[1]))
            h.outline(rng, star, "blue", w=0.65, tone=0.85, amp=0.0)
            h.dot(fx, fy - fh, 1.2 * k, "coral" if fc[0] == "sun" else "sun", 1.0, knock=True)
    elif style == "round":
        r = 14.0 * k
        p = blob(pr, x, y - r * 0.85, r * 1.1, r * 0.95, 0.0)
        draw_leaf(p, 1.0)
        h.clip(p, lambda: [h.tint(blob(pr, x + rd(pr, -r, r), y - r + rd(pr, -r * 0.6, r * 0.5), r * 0.35, r * 0.25), "blue", 0.25, fine=fine) for _ in range(6)])
    elif style == "spiky":
        for n in range(7):
            a = -math.pi * 0.5 + (n - 3) * 0.13 + rd(pr, -0.05, 0.05)
            length = rd(pr, 22.0, 34.0) * k
            poly = ribbon(
                curve([(x + (n - 3) * 1.5 * k, y), (x + math.cos(a) * length * 0.5, y + math.sin(a) * length * 0.5), (x + math.cos(a) * length, y + math.sin(a) * length)], 2),
                lambda t: 5.0 * k * (1.0 - t * 0.85),
            )
            draw_leaf(poly, rd(pr, 0.85, 1.15))


def _draw_primitive(h: RisoHand, rng: SeededRng, s: Dict[str, Any], ctx: Dict[str, Any]) -> None:
    t = shape_type(s)
    if not t:
        return
    pl: Placer = ctx["pl"]
    fine = ctx["fine"]
    lw = ctx["lw"]
    col_fn: Callable[[Optional[str]], List[Any]] = ctx["col"]

    if t == "box":
        u, v, z, w, d, height = s["box"]
        i, j, wi, dj = pl.rect(u, v, w, d)
        zz = pl.w(u, v, z)[2]
        col = col_fn(s.get("color"))
        top_col = col_fn(s["top"]) if s.get("top") else None
        if not s.get("soft"):
            box_at(
                h,
                rng,
                i,
                j,
                wi,
                dj,
                zz,
                height,
                col,
                shadow=bool(s.get("shadow", False)),
                top_col=top_col,
                line=s.get("line", True) is not False,
                lw=lw or 1.0,
                fine=fine,
            )
            return
        # Soft cushion/loaf: Chaikin-rounded silhouette.
        if s.get("shadow") and height > 0.05:
            shadow_box(h, i, j, wi, dj, zz, height, 0.20)
        a = h.p(i, j, zz + height)
        b = h.p(i + wi, j, zz + height)
        c = h.p(i + wi, j + dj, zz + height)
        d_pt = h.p(i, j + dj, zz + height)
        b0 = h.p(i + wi, j, zz)
        c0 = h.p(i + wi, j + dj, zz)
        d0 = h.p(i, j + dj, zz)
        hull = loop([a, b, b0, c0, d0, d_pt], 2)
        top = loop([a, b, c, d_pt], 2)
        _paint(h, hull, col, 0.18, fine)
        h.clip(hull, lambda: h.tint([b, b0, c0, c], "blue", 0.24, fine=fine))
        _paint(h, top, top_col or col, 0.0, fine)
        h.outline(rng, hull, "blue", w=lw or 1.0, tone=0.9, amp=0.15, fine=fine)
        h.line(rng, curve([lerp_p(d_pt, c, 0.1), c, lerp_p(c, b, 0.12)], 2), "blue", w=0.8, tone=0.6, amp=0.1)

    elif t == "legs":
        u, v, z, w, d, height = s["legs"]
        thick = float(s.get("thick", s.get("t", 0.07)))
        inset = float(s.get("inset", 0.05))
        col = col_fn(s.get("color"))
        for du, dv in (
            (inset, inset),
            (w - inset - thick, inset),
            (inset, d - inset - thick),
            (w - inset - thick, d - inset - thick),
        ):
            i, j, wi, dj = pl.rect(u + du, v + dv, thick, thick)
            zz = pl.w(u, v, z)[2]
            box_at(h, rng, i, j, wi, dj, zz, height, col, shadow=False, lw=0.7, fine=True)

    elif t == "cyl":
        u, v, z, r, height = s["cyl"]
        _draw_cone(h, rng, ctx, u, v, z, r, r, height, col_fn(s.get("color")), col_fn(s["top"]) if s.get("top") else None, s)

    elif t == "cone":
        u, v, z, r0, r1, height = s["cone"]
        _draw_cone(h, rng, ctx, u, v, z, r0, r1, height, col_fn(s.get("color")), col_fn(s["top"]) if s.get("top") else None, s)

    elif t == "ball":
        u, v, z, r = s["ball"]
        x, y = h.p(*pl.w(u, v, z))
        rr = r * 36.0
        col = col_fn(s.get("color"))
        if s.get("shadow"):
            gx, gy = h.p(*pl.w(u, v, 0.0))
            shadow_at(h, gx + rr * 0.3, gy + 2.0, rr * 0.9, rr * 0.4, 0.20)
        poly = circ(x, y, rr, max(16, int(round(rr * 1.4))))
        _paint(h, poly, col, 0.0, fine)
        h.clip(poly, lambda: h.tint(circ(x + rr * 0.45, y + rr * 0.5, rr * 1.05), "blue", 0.30, fine=fine))
        h.dot(x - rr * 0.35, y - rr * 0.38, max(0.8, rr * 0.18), "paper", 1.0, knock=True)
        h.outline(rng, poly, "blue", w=lw or 0.9, tone=0.9, amp=0.15, fine=fine)

    elif t == "pole":
        u0, v0, z0, u1, v1, z1 = s["pole"]
        col = col_fn(s.get("color"))
        a = h.p(*pl.w(u0, v0, z0))
        b = h.p(*pl.w(u1, v1, z1))
        w = float(s.get("w", 2.0))
        h.line(rng, [a, b], "blue", w=w + 1.2, amp=0.05)
        if col[0] != "black":
            h.line(rng, [a, b], col[0], w=max(0.6, w - 0.6), amp=0.05, tone=max(col[1], 0.8))

    elif t == "panel":
        u, v, z, w, height = s["panel"]

        def on_plane(a_val: float, b_val: float, dv: float = 0.0) -> Point:
            return h.p(*pl.w(a_val, v + dv, b_val))

        def rect_on(a0: float, b0: float, a1: float, b1: float, dv: float = 0.0) -> List[Point]:
            if s.get("round"):
                return [
                    on_plane((a0 + a1) * 0.5 + x * (a1 - a0) * 0.5, (b0 + b1) * 0.5 + y * (b1 - b0) * 0.5, dv)
                    for x, y in arc_pts(0, 0, 1, 1, 0, TAU, 36)[:-1]
                ]
            return [on_plane(a0, b0, dv), on_plane(a1, b0, dv), on_plane(a1, b1, dv), on_plane(a0, b1, dv)]

        poly = rect_on(u, z, u + w, z + height, 0.0)
        shade = 0.12 if pl.nw else 0.0
        if s.get("drop"):
            h.tint([(p[0] + 3.0, p[1] + 3.0) for p in poly], "blue", 0.22, over_transparent=h.transparent_sprite)

        inner = poly
        m = float(s.get("inset", 0.08)) if s.get("frame") else 0.0
        a0, b0, a1, b1 = u + m, z + m, u + w - m, z + height - m
        is_glass = bool(
            s.get("transparent")
            or s.get("color") == "glass"
            or (ctx["kind"].get("colors") or {}).get(s.get("color")) == "glass"
        )
        if s.get("frame"):
            inner = rect_on(a0, b0, a1, b1, 0.005)
            if is_glass:
                # Draw only the frame border (thick ink outline) so interior products stay visible through glass!
                fcol = col_fn(s["frame"])
                h.outline(rng, poly, fcol[0] if fcol[0] != "paper" else "blue", w=2.2, tone=max(0.75, fcol[1]), amp=0.06, fine=fine)
            else:
                _paint(h, poly, col_fn(s["frame"]), shade, fine)
        if s.get("color"):
            c_val = col_fn(s["color"])
            if is_glass:
                # Translucent riso glass: overprint only (no paper knockout) so stocked goods show through!
                h.tint(inner, c_val[0], c_val[1], fine=fine)
                if len(c_val) > 2 and c_val[2]:
                    h.tint(inner, c_val[2], c_val[3], fine=fine)
                if shade > 0.0:
                    h.tint(inner, "blue", shade * 0.5, fine=fine)
            else:
                _paint(h, inner, c_val, shade, fine)
        if s.get("shine"):
            def _shine() -> None:
                p = lambda aa, bb: on_plane(lerp(a0, a1, aa), lerp(b0, b1, bb), 0.01)
                h.line(rng, [p(0.14, 0.20), p(0.56, 0.94)], "paper", w=2.6, amp=0.0, tone=0.85)
                h.line(rng, [p(0.44, 0.14), p(0.74, 0.64)], "paper", w=1.4, amp=0.0, tone=0.70)
            h.clip(inner, _shine)
        h.outline(rng, poly, "blue", w=lw or 1.0, tone=0.9, amp=0.15, fine=fine)
        if inner is not poly:
            h.outline(rng, inner, "blue", w=0.7, tone=0.65, amp=0.05, fine=fine)

    elif t == "plant":
        _draw_plant(h, rng, ctx, s)

    elif t == "books":
        u, v, z, length = s["books"][:4]
        hh = s["books"][4] if len(s["books"]) > 4 else 0.45
        br = stream_of("books", ctx["item"].get("id", ""), u, v, z)
        pal = ["coral", "teal", "blue", "mustard", "pink", "navy", "sage", "cream", "terracotta"]
        pos = u + 0.03
        while pos < u + length - 0.08:
            bw = rd(br, 0.08, 0.16)
            bh = hh * rd(br, 0.62, 1.0)
            i, j, wi, dj = pl.rect(pos, v, bw, 0.30)
            zz = pl.w(pos, v, z)[2]
            box_at(h, br, i, j, wi, dj, zz, bh, SWATCH[pk(br, pal)], shadow=False, lw=0.6, fine=True)
            pos += bw + (rd(br, 0.04, 0.12) if br() < 0.15 else 0.01)

    elif t == "flat":
        u, v, z, w, d = s["flat"]
        i, j, wi, dj = pl.rect(u, v, w, d)
        zz = pl.w(u, v, z + 0.01)[2]
        col = col_fn(s.get("color"))
        col2 = col_fn(s.get("color2", "cream"))
        pat = s.get("pattern", "plain")
        poly = h.tile(i, j, wi, dj, zz)
        if pat == "round":
            ci, cj = i + wi * 0.5, j + dj * 0.5
            poly = [h.p(ci + math.cos(k / 40.0 * TAU) * wi * 0.5, cj + math.sin(k / 40.0 * TAU) * dj * 0.5, zz) for k in range(40)]
        _paint(h, poly, col, 0.0, fine)

        def _pat() -> None:
            if pat == "stripes":
                a = 0.25
                while a < w:
                    i2, j2, ww, dd = pl.rect(u + a, v, 0.18, d)
                    h.tint(h.tile(i2, j2, ww, dd, zz), col2[0], col2[1])
                    a += 0.45
            elif pat == "border":
                i2, j2, ww, dd = pl.rect(u + 0.18, v + 0.18, max(0.05, w - 0.36), max(0.05, d - 0.36))
                h.outline(rng, h.tile(i2, j2, ww, dd, zz), col2[0], w=2.0, tone=max(0.8, col2[1]))
            elif pat == "check":
                a = 0.0
                while a < w:
                    b = 0.0
                    while b < d:
                        if (int(round(a / 0.4)) + int(round(b / 0.4))) % 2:
                            i2, j2, ww, dd = pl.rect(u + a, v + b, 0.4, 0.4)
                            h.tint(h.tile(i2, j2, ww, dd, zz), col2[0], col2[1])
                        b += 0.4
                    a += 0.4

        h.clip(poly, _pat)
        h.outline(rng, poly, "blue", w=0.9, tone=0.75, amp=0.15, fine=fine)

    elif t == "glow":
        u, v, z = s["glow"]
        x, y = h.p(*pl.w(u, v, z))
        r = float(s.get("r", 1.2))
        col = col_fn(s.get("color", "yellow"))
        h.glow(x, y, r * 36.0, r * 26.0, "sun" if col[0] == "paper" else col[0], float(s.get("tone", 0.45)))

    elif t == "lines":
        col = col_fn(s.get("color", "black"))
        pts = [h.p(*pl.w(pt[0], pt[1], pt[2] if len(pt) > 2 else 0.0)) for pt in s["lines"]]
        dash = [2.5, 2.0] if s.get("dash") else None
        h.line(
            rng,
            pts,
            "paper" if col[0] == "paper" else col[0],
            w=float(s.get("w", 1.0)),
            tone=max(0.85, col[1]),
            amp=0.08,
            dash=dash,
            cap="butt" if dash else "round",
        )

    elif t == "dot":
        pt = s["dot"]
        x, y = h.p(*pl.w(pt[0], pt[1], pt[2] if len(pt) > 2 else 0.0))
        col = col_fn(s.get("color", "black"))
        h.dot(x, y, float(s.get("r", 1.5)), col[0], max(col[1], 0.85), knock=True)


def _shape_base_z(s: Dict[str, Any]) -> float:
    for key in ("box", "cyl", "cone", "legs", "ball"):
        if key in s:
            return float(s[key][2])
    if "pole" in s:
        return min(float(s["pole"][2]), float(s["pole"][5]))
    return 0.0


def draw_thing(
    h: RisoHand,
    rng: SeededRng,
    kind: Dict[str, Any],
    item: Dict[str, Any],
    fine: Optional[bool] = None,
    lw: Optional[float] = None,
) -> None:
    size = item.get("size") or kind["size"]
    pl = Placer(item["at"], size)
    ctx = {
        "pl": pl,
        "col": lambda name: _resolve_color(kind, item, name),
        "item": item,
        "kind": kind,
        "fine": kind.get("fine", False) if fine is None else fine,
        "lw": lw,
    }
    shapes = [_resolve_shape(s0, kind, item) for s0 in kind.get("shapes", [])]
    has_floor_shape_shadow = any(s.get("shadow") and _shape_base_z(s) <= 0.05 for s in shapes)
    if kind.get("place") not in ("wall", "rug") and kind.get("shadow", True) is not False and (not has_floor_shape_shadow or kind.get("name") == "shopping-cart"):
        w, d = pl.size(size[0], size[1])
        fallback_tone = 0.13 if kind.get("name") == "shopping-cart" else 0.20
        shadow_box(h, float(item["at"].get("i", 0.0)), float(item["at"].get("j", 0.0)), w, d, float(item["at"].get("z", 0.0)), float(kind.get("height", 1.0)) * 0.7, fallback_tone)
    for s in shapes:
        _draw_primitive(h, rng, s, ctx)


def screen_box(kind: Dict[str, Any], item: Dict[str, Any]) -> Dict[str, int]:
    size = item.get("size") or kind["size"]
    pl = Placer(item["at"], size)
    is_wall = kind.get("place") == "wall"
    hh = size[1] if is_wall else float(kind.get("height", 1.0))
    dd = 0.3 if is_wall else float(size[1])
    pts: List[Point] = []
    for u in (0.0, float(size[0])):
        for v in (0.0, dd):
            for z in (0.0, hh + 0.5):
                pts.append(iso(*pl.w(u, v, z)))
    x0, y0, x1, y1 = bbox(pts)
    reach = kind.get("reach") or {}
    pad = 22
    return {
        "x0": int(math.floor(x0 - max(pad, reach.get("l", 0) + 12))),
        "y0": int(math.floor(y0 - max(pad, reach.get("t", 0) + 14))),
        "x1": int(math.ceil(x1 + max(pad, reach.get("r", 0) + 22))),
        "y1": int(math.ceil(y1 + max(pad, reach.get("b", 0) + 22))),
    }
