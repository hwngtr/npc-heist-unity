"""
Cutaway room shell, box primitives, cast shadows, walls, windows, and lighting
for the 2D Riso Press engine.
"""

from __future__ import annotations

import math
from typing import Callable, Dict, List, Optional, Sequence, Tuple

from press import (
    Point,
    RisoHand,
    SeededRng,
    blob,
    centroid,
    curve,
    ell,
    iso,
    iso_x,
    iso_y,
    lerp,
    loop,
    rd,
    stream_of,
    TZ,
)

ROOM = 9
PITCH = 9
WALL_H = 3.2
LOW_H = 0.85
GAP = (3.55, 5.45)
DEFAULT_EDGES = {"ne": "wall", "nw": "wall", "se": "cut", "sw": "cut"}


# ---------------- Shadow & Box primitives ----------------

def shadow_at(h: RisoHand, x: float, y: float, rx: float, ry: float, tone: float = 0.22) -> None:
    h.tint(ell(x, y, rx, ry), "blue", tone, over_transparent=h.transparent_sprite)


def shadow_box(
    h: RisoHand,
    i: float,
    j: float,
    w: float,
    d: float,
    z: float,
    height: float,
    tone: float = 0.20,
) -> None:
    k = min(height, 2.0) * 0.45
    pts = [
        (p[0] + 4.0, p[1] + 2.0)
        for p in (
            h.p(i + k, j + k * 0.5, z),
            h.p(i + w + k, j + k * 0.5, z),
            h.p(i + w + k, j + d + k * 0.5, z),
            h.p(i + k, j + d + k * 0.5, z),
        )
    ]
    h.tint(pts, "blue", tone, over_transparent=h.transparent_sprite)


def box_at(
    h: RisoHand,
    rng: SeededRng,
    i: float,
    j: float,
    w: float,
    d: float,
    z: float,
    height: float,
    col: Sequence,
    shadow: bool = True,
    shadow_tone: float = 0.20,
    top_col: Optional[Sequence] = None,
    line: bool = True,
    lw: float = 1.0,
    fine: Optional[bool] = None,
    half: float = 0.18,
    full: float = 0.40,
) -> Dict[str, List[Point]]:
    ink = col[0]
    tone = col[1]
    ink2 = col[2] if len(col) > 2 else None
    tone2 = col[3] if len(col) > 3 else 0.0

    if shadow and height > 0.05:
        shadow_box(h, i, j, w, d, z, height, shadow_tone)

    top = h.tile(i, j, w, d, z + height)
    se = h.face_j(i + w, j, d, z, z + height)
    sw = h.face_i(i, j + d, w, z, z + height)

    def paint_face(face: List[Point], extra_blue: float) -> None:
        h.fill(face, ink, tone, fine=fine)
        if ink2 and tone2 > 0.0:
            h.tint(face, ink2, tone2, fine=fine)
        if extra_blue > 0.0:
            h.tint(face, "blue", extra_blue, fine=fine)

    paint_face(sw, half)
    paint_face(se, full)
    if top_col is not None:
        h.fill(top, top_col[0], top_col[1], fine=fine)
        if len(top_col) > 2 and top_col[2]:
            h.tint(top, top_col[2], top_col[3], fine=fine)
    else:
        paint_face(top, 0.0)

    if line:
        h.outline(rng, top, "blue", w=lw, tone=0.9, amp=0.15, fine=fine)
        h.line(rng, [top[3], sw[0], sw[1], se[1], top[2]], "blue", w=lw, tone=0.9, amp=0.15, fine=fine)
        h.line(rng, [top[2], sw[1]], "blue", w=lw, tone=0.9, amp=0.15, fine=fine)

    return {"top": top, "se": se, "sw": sw}


# ---------------- Room Slab & Floors ----------------

def slab(h: RisoHand, rng: SeededRng, rm: Dict, edges: Dict) -> List[Point]:
    w = rm["w"]
    d = rm["d"]
    th = 0.40
    top = h.tile(0, 0, w, d, 0)
    se = h.face_j(w, 0, d, -th, 0)
    sw = h.face_i(0, d, w, -th, 0)
    if edges.get("se") == "cut":
        h.fill(se, "blue", 0.62)
        h.line(rng, [se[0], se[1], se[2]], "blue", w=1.0, tone=0.85, amp=0.25)
    if edges.get("sw") == "cut":
        h.fill(sw, "blue", 0.40)
        h.line(rng, [sw[0], sw[3], sw[2]], "blue", w=1.0, tone=0.85, amp=0.25)
    h.knock(top)
    return top


def floor_of(h: RisoHand, rng: SeededRng, rm: Dict, floor: Dict) -> List[Point]:
    w = rm["w"]
    d = rm["d"]
    poly = h.tile(0, 0, w, d, 0)
    style = floor.get("style", "planks")
    ink = floor.get("ink", "sun")
    tone = floor.get("tone", 0.25)
    ink2 = floor.get("ink2")
    tone2 = floor.get("tone2", 0.12)

    h.fill(poly, ink, tone)
    if ink2 and tone2 > 0.0 and style not in ("checker", "tiles"):
        h.tint(poly, ink2, tone2)

    def _pattern() -> None:
        if style == "planks":
            r = 0
            while r * 0.6 < d:
                j = r * 0.6
                h.line(rng, [h.p(0, j, 0), h.p(w, j, 0)], "blue", w=0.7, tone=0.45, amp=0.12)
                i = rd(rng, 0.0, 2.5)
                while i < w:
                    h.line(rng, [h.p(i, j, 0), h.p(i, j + 0.6, 0)], "blue", w=0.7, tone=0.40, amp=0.05)
                    i += rd(rng, 2.2, 4.2)
                if rng() < 0.35:
                    h.tint(h.tile(rd(rng, 0.0, max(0.5, w - 3.0)), j, rd(rng, 1.5, 3.0), 0.6, 0), ink, 0.12)
                r += 1
        elif style in ("tiles", "checker"):
            for i in range(int(w)):
                for j in range(int(d)):
                    if style == "checker":
                        if (i + j) % 2 == 1:
                            h.tint(h.tile(i, j, 1, 1, 0), ink2 or "blue", floor.get("tone2", 0.22))
                    else:
                        if rng() < 0.22:
                            h.tint(h.tile(i, j, 1, 1, 0), ink2 or "blue", floor.get("tone2", 0.09))
            for i in range(int(w) + 1):
                h.line(rng, [h.p(i, 0, 0), h.p(i, d, 0)], "blue", w=0.6, tone=0.40, amp=0.05)
            for j in range(int(d) + 1):
                h.line(rng, [h.p(0, j, 0), h.p(w, j, 0)], "blue", w=0.6, tone=0.40, amp=0.05)
        elif style == "concrete":
            h.speckle(rng, poly, ink2 or "blue", 220, 0.4, 1.2, 0.28)
            for i in range(0, int(w) + 1, 3):
                h.line(rng, [h.p(i, 0, 0), h.p(i, d, 0)], "blue", w=0.65, tone=0.35, amp=0.08)
            for j in range(0, int(d) + 1, 3):
                h.line(rng, [h.p(0, j, 0), h.p(w, j, 0)], "blue", w=0.65, tone=0.35, amp=0.08)
            for _ in range(3):
                a0, a1 = rd(rng, 1.0, w - 1.0), rd(rng, 1.0, d - 1.0)
                pts = curve(
                    [
                        h.p(a0, a1, 0),
                        h.p(a0 + rd(rng, -0.9, 0.9), a1 + rd(rng, 0.4, 1.3), 0),
                        h.p(a0 + rd(rng, -1.3, 1.3), a1 + rd(rng, 1.3, 2.2), 0),
                    ],
                    2,
                )
                h.line(rng, pts, "blue", w=0.6, tone=0.32)
        elif style == "carpet":
            h.speckle(rng, poly, ink2 or "blue", 650, 0.4, 1.0, 0.30)

    h.clip(poly, _pattern)
    h.outline(rng, poly, "blue", w=1.0, tone=0.85, amp=0.25)
    return poly


# ---------------- Walls, Windows, Doors, Bulbs ----------------

def wall_pt(h: RisoHand, side: str, pos: float, z: float, off: float = 0.0) -> Point:
    return h.p(pos, -off, z) if side == "ne" else h.p(-off, pos, z)


def wall_rect(
    h: RisoHand, side: str, p0: float, p1: float, z0: float, z1: float, off: float = 0.02
) -> List[Point]:
    return [
        wall_pt(h, side, p0, z0, off),
        wall_pt(h, side, p1, z0, off),
        wall_pt(h, side, p1, z1, off),
        wall_pt(h, side, p0, z1, off),
    ]


def cloud(h: RisoHand, rng: SeededRng, x: float, y: float, s: float) -> None:
    pts: List[Point] = []
    for k in range(7):
        t = k / 6.0 * math.pi
        pts.append((x - s + k * s * 2.0 / 6.0, y - s * 0.4 - math.sin(t) * s * 0.55 - rd(rng, 0, s * 0.15)))
    pts.extend([(x + s, y), (x - s, y)])
    q = loop(pts, 2)
    h.fill(q, "paper", 1.0)
    h.outline(rng, q, "blue", w=0.9, tone=0.7, amp=0.35)


def back_walls(h: RisoHand, rng: SeededRng, rm: Dict, wall: Dict, sides: Sequence[str]) -> None:
    w = rm["w"]
    d = rm["d"]
    height = wall.get("h", WALL_H)
    ink = wall.get("ink", "coral")
    tone = wall.get("tone", 0.20)
    style = wall.get("style", "plain")
    ink2 = wall.get("ink2")
    tone2 = wall.get("tone2", 0.14)

    all_faces = [
        ("nw", h.face_j(0, 0, d, 0, height), d, 0.14),
        ("ne", h.face_i(0, 0, w, 0, height), w, 0.04),
    ]
    for side, face, length, shade in all_faces:
        if side not in sides:
            continue
        h.fill(face, ink, tone)
        if ink2:
            h.tint(face, ink2, tone2)

        def _detail() -> None:
            if style == "plank":
                p = 0.7
                while p < length:
                    h.line(rng, [wall_pt(h, side, p, 0), wall_pt(h, side, p, height)], "blue", w=0.8, tone=0.45, amp=0.2)
                    p += 0.7
            elif style == "stripe":
                p = 0.0
                while p < length:
                    h.tint(wall_rect(h, side, p, p + 0.5, 0, height, 0.0), ink2 or ink, 0.18)
                    p += 1.0
            elif style == "tile":
                p = 0.0
                while p <= length + 1e-6:
                    h.line(rng, [wall_pt(h, side, p, 0), wall_pt(h, side, p, height * 0.52)], "blue", w=0.6, tone=0.35, amp=0.1)
                    p += 0.5
                z = 0.0
                while z <= height * 0.52 + 1e-6:
                    h.line(rng, [wall_pt(h, side, 0, z), wall_pt(h, side, length, z)], "blue", w=0.6, tone=0.35, amp=0.1)
                    z += 0.5
                h.line(rng, [wall_pt(h, side, 0, height * 0.52), wall_pt(h, side, length, height * 0.52)], "blue", w=1.0, tone=0.7)
            h.tint(face, "blue", shade)
            h.grad(face, "blue", 0.12, 0.0, wall_pt(h, side, length * 0.5, 0), wall_pt(h, side, length * 0.5, height * 0.45), 6)

        h.clip(face, _detail)


def window_on(h: RisoHand, rng: SeededRng, side: str, pos: float, z: float, w: float, height: float) -> None:
    poly = wall_rect(h, side, pos - w * 0.5, pos + w * 0.5, z, z + height, 0.03)
    h.fill(poly, "blue", 0.14)
    h.tint(poly, "teal", 0.10)

    def _sky() -> None:
        c = centroid(poly)
        rr = stream_of("window", side, pos)
        cloud(h, rr, c[0] + rd(rr, -10.0, 10.0), c[1] - height * 6.0, 9.0 + w * 3.0)
        t = wall_pt(h, side, pos + w * 0.3, z, 0.03)
        h.fill(blob(rr, t[0], t[1] - 2.0, 16.0, 10.0, 0.0), "teal", 0.60)

    h.clip(poly, _sky)
    h.line(rng, [wall_pt(h, side, pos, z, 0.04), wall_pt(h, side, pos, z + height, 0.04)], "paper", w=2.4, amp=0.1)
    h.line(rng, [wall_pt(h, side, pos - w * 0.5, z + height * 0.55, 0.04), wall_pt(h, side, pos + w * 0.5, z + height * 0.55, 0.04)], "paper", w=2.4, amp=0.1)
    h.outline(rng, poly, "blue", w=1.4, tone=0.9, amp=0.2)
    sill = [
        wall_pt(h, side, pos - w * 0.5 - 0.1, z, 0.04),
        wall_pt(h, side, pos + w * 0.5 + 0.1, z, 0.04),
        wall_pt(h, side, pos + w * 0.5 + 0.1, z, 0.22),
        wall_pt(h, side, pos - w * 0.5 - 0.1, z, 0.22),
    ]
    h.fill(sill, "paper", 1.0)
    h.outline(rng, sill, "blue", w=0.9, tone=0.8)


def sun_patch(h: RisoHand, side: str, pos: float, w: float) -> None:
    if side == "ne":
        poly = [
            h.p(pos - w * 0.5 + 0.3, 0.15, 0),
            h.p(pos + w * 0.5 + 0.3, 0.15, 0),
            h.p(pos + w * 0.5 + 1.6, 3.2, 0),
            h.p(pos - w * 0.5 + 1.6, 3.2, 0),
        ]
    else:
        poly = [
            h.p(0.15, pos - w * 0.5 + 0.2, 0),
            h.p(0.15, pos + w * 0.5 + 0.2, 0),
            h.p(3.4, pos + w * 0.5 + 1.2, 0),
            h.p(3.4, pos - w * 0.5 + 1.2, 0),
        ]
    h.tint(poly, "sun", 0.22)


def door_on(h: RisoHand, rng: SeededRng, side: str, pos: float, w: float = 1.3, height: float = 2.35) -> None:
    poly = wall_rect(h, side, pos - w * 0.5, pos + w * 0.5, 0.0, height, 0.03)
    h.fill(poly, "teal", 0.18)
    h.tint(poly, "blue", 0.14)
    inset = wall_rect(h, side, pos - w * 0.5 + 0.14, pos + w * 0.5 - 0.14, 0.25, height - 0.18, 0.035)
    h.outline(rng, inset, "blue", w=0.8, tone=0.65, amp=0.1)
    h.line(rng, [wall_pt(h, side, pos, 0.0, 0.04), wall_pt(h, side, pos, height, 0.04)], "blue", w=1.0, tone=0.85, amp=0.05)
    h.outline(rng, poly, "blue", w=1.3, tone=0.95)
    mat = h.tile(0.05, pos - 0.7, 0.85, 1.4, 0.01) if side == "nw" else h.tile(pos - 0.7, 0.05, 1.4, 0.85, 0.01)
    h.fill(mat, "coral", 0.42)
    h.outline(rng, mat, "blue", w=0.8, tone=0.75)


def wall_trim(h: RisoHand, rng: SeededRng, rm: Dict, wall: Dict, sides: Sequence[str]) -> None:
    w = rm["w"]
    d = rm["d"]
    height = wall.get("h", WALL_H)
    ink = wall.get("ink", "coral")
    tone = wall.get("tone", 0.20)
    th = 0.14
    if "nw" in sides:
        skirt = wall_rect(h, "nw", 0, d, 0, 0.16, 0.01)
        h.fill(skirt, "paper", 1.0)
        h.tint(skirt, "blue", 0.20)
        h.grad([h.p(0, 0, 0), h.p(1.4, 1.4, 0), h.p(1.4, d, 0), h.p(0, d, 0)], "blue", 0.20, 0.0, h.p(0, d * 0.5, 0), h.p(1.2, d * 0.5, 0), 5)
        cap = [h.p(0, 0, height), h.p(0, d, height), h.p(-th, d, height), h.p(-th, 0, height)]
        h.fill(cap, ink, tone * 0.4)
        h.outline(rng, cap, "blue", w=1.0, tone=0.9, amp=0.2)
        h.outline(rng, h.face_j(0, 0, d, 0, height), "blue", w=1.1, tone=0.9, amp=0.25)
    if "ne" in sides:
        skirt = wall_rect(h, "ne", 0, w, 0, 0.16, 0.01)
        h.fill(skirt, "paper", 1.0)
        h.tint(skirt, "blue", 0.08)
        h.grad([h.p(0, 0, 0), h.p(w, 0, 0), h.p(w, 1.1, 0), h.p(1.1, 1.1, 0)], "blue", 0.16, 0.0, h.p(w * 0.5, 0, 0), h.p(w * 0.5, 1.0, 0), 5)
        cap = [h.p(0, 0, height), h.p(w, 0, height), h.p(w, -th, height), h.p(0, -th, height)]
        h.fill(cap, ink, tone * 0.4)
        h.outline(rng, cap, "blue", w=1.0, tone=0.9, amp=0.2)
        h.outline(rng, h.face_i(0, 0, w, 0, height), "blue", w=1.1, tone=0.9, amp=0.25)


def low_wall(h: RisoHand, rng: SeededRng, rm: Dict, side: str, paint: Dict) -> None:
    length = rm["w"] if side == "ne" else rm["d"]
    col = [paint.get("ink", "coral"), max(0.14, paint.get("tone", 0.22)) + 0.06]
    t = 0.07
    for a, b in ((0.0, GAP[0]), (GAP[1], length)):
        if side == "ne":
            box_at(h, rng, a, -t, b - a, 2 * t, 0.0, LOW_H, col, shadow=False, lw=0.9, top_col=["paper", 1.0])
        else:
            box_at(h, rng, -t, a, 2 * t, b - a, 0.0, LOW_H, col, shadow=False, lw=0.9, top_col=["paper", 1.0])
    if side == "ne":
        foot = [h.p(0, t, 0), h.p(length, t, 0), h.p(length, 0.75, 0), h.p(0.75, 0.75, 0)]
        h.grad(foot, "blue", 0.12, 0.0, h.p(length * 0.5, 0, 0), h.p(length * 0.5, 0.7, 0), 4)
    else:
        foot = [h.p(t, 0, 0), h.p(0.75, 0.75, 0), h.p(0.75, length, 0), h.p(t, length, 0)]
        h.grad(foot, "blue", 0.12, 0.0, h.p(0, length * 0.5, 0), h.p(0.7, length * 0.5, 0), 4)


def bulb_on(h: RisoHand, rng: SeededRng, side: str, pos: float, lit: bool = True) -> None:
    top = wall_pt(h, side, pos, WALL_H, 0.02)
    at = wall_pt(h, side, pos, WALL_H - 1.0, 0.5)
    cord = curve([top, (lerp(top[0], at[0], 0.55) + 2.0, lerp(top[1], at[1], 0.4)), (at[0], at[1] - 6.0)], 2)
    h.line(rng, cord, "black", w=0.9, amp=0.1)
    if lit:
        h.glow(at[0], at[1] + 2.0, 26.0, 22.0, "sun", 0.45)
    cap = [(at[0] - 2, at[1] - 7), (at[0] + 2, at[1] - 7), (at[0] + 2, at[1] - 3), (at[0] - 2, at[1] - 3)]
    h.fill(cap, "black", 0.9)
    glass = ell(at[0], at[1] + 1.0, 4.0, 4.6)
    h.fill(glass, "sun", 0.55 if lit else 0.10)
    h.outline(rng, glass, "blue", w=0.8, tone=0.9, amp=0.05)


def draw_shell(
    h: RisoHand,
    rng: SeededRng,
    rm: Dict,
    edges: Optional[Dict] = None,
    draw_low_walls: bool = True,
) -> None:
    e = edges or DEFAULT_EDGES
    full = [sd for sd in ("nw", "ne") if e.get(sd) == "wall"]
    slab(h, rng, rm, e)
    floor_of(h, rng, rm, rm.get("floor", {}))
    wall = rm.get("walls", {})
    if full:
        back_walls(h, rng, rm, wall, full)
        for win in rm.get("windows", []):
            if win.get("side") in full:
                window_on(h, rng, win["side"], win["pos"], win.get("z", 1.1), win.get("w", 2.2), win.get("h", 1.5))
        door = rm.get("door")
        if door and door.get("side") in full:
            door_on(h, rng, door["side"], door["pos"])
        wall_trim(h, rng, rm, wall, full)
    if draw_low_walls:
        for sd in ("ne", "nw"):
            if e.get(sd) == "low":
                low_wall(h, rng, rm, sd, wall)
    for win in rm.get("windows", []):
        if win.get("side") in full:
            sun_patch(h, win["side"], win["pos"], win.get("w", 2.2))
    bulb = rm.get("bulb")
    if bulb and e.get(bulb.get("side")) == "wall":
        bulb_on(h, rng, bulb["side"], bulb["pos"], True)


def room_edges(rooms: Sequence[Dict]) -> Dict[str, Dict[str, str]]:
    by_coord = {(rm["at"][0], rm["at"][1]): rm for rm in rooms}
    result: Dict[str, Dict[str, str]] = {}
    for rm in rooms:
        gx, gy = rm["at"]
        result[rm["id"]] = {
            "nw": "low" if (gx - 1, gy) in by_coord else "wall",
            "ne": "low" if (gx, gy - 1) in by_coord else "wall",
            "se": "join" if (gx + 1, gy) in by_coord else "cut",
            "sw": "join" if (gx, gy + 1) in by_coord else "cut",
        }
    return result


def room_origin(at: Sequence[int]) -> Point:
    return iso(at[0] * PITCH, at[1] * PITCH, 0.0)


def room_bounds(rm: Dict) -> Dict[str, float]:
    w = rm["w"]
    d = rm["d"]
    return {
        "x0": iso_x(0, d) - 36.0,
        "y0": -WALL_H * TZ - 42.0,
        "x1": iso_x(w, 0) + 36.0,
        "y1": iso_y(w, d, 0) + 36.0,
    }
