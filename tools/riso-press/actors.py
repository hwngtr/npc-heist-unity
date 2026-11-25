"""
Riso-printed characters, CCTV camera, loot glints, and seamless UI Bayer tiles.
Follows the crisp silhouette + paper highlight dot + 1px ink outline grammar.
"""

from __future__ import annotations

import math
from typing import Dict, Tuple

import cairo

from press import (
    INK,
    PAPER,
    RisoHand,
    arc_pts,
    circ,
    dither_tile_surface,
    ell,
    loop,
    make_surface,
    star_pts,
    stream_of,
)
from world import box_at, shadow_at


def _draw_bot_body(
    h: RisoHand,
    rng,
    cx: float,
    cy: float,
    radius: float,
    ink: str,
    tone: float,
    ink2: str | None = None,
    tone2: float = 0.0,
    cap_ink: str = "sun",
    cap_tone: float = 0.92,
    badge_ink: str | None = None,
    crouch: bool = False,
    stride: int = 0,
    satchel_ink: str | None = None,
) -> None:
    ry = radius * (0.76 if crouch else 1.0)
    bob = -2.2 if stride != 0 else 0.0
    by = cy - ry * 0.92 + bob

    # Ground shadow (tightens slightly during stride lift)
    sh_scale = 0.92 if stride != 0 else 1.0
    shadow_at(h, cx + 4.0, cy + 2.0, radius * 1.05 * sh_scale, radius * 0.48 * sh_scale, 0.24)

    # Animated stride feet
    left_dx = -radius * 0.36 + (3.2 * stride)
    left_dy = -1.5 + (-2.4 if stride > 0 else 0.8 if stride < 0 else 0.0)
    right_dx = radius * 0.36 - (3.2 * stride)
    right_dy = -1.5 + (-2.4 if stride < 0 else 0.8 if stride > 0 else 0.0)
    for dx, dy in ((left_dx, left_dy), (right_dx, right_dy)):
        foot = ell(cx + dx, cy + dy, radius * 0.25, radius * 0.15, 12)
        h.fill(foot, "black", 0.92)
        h.outline(rng, foot, "blue", w=1.0, tone=0.95, amp=0.05)

    # Round bot silhouette
    body = ell(cx, by, radius, ry, 28)
    h.fill(body, ink, tone)
    if ink2 and tone2 > 0:
        h.tint(body, ink2, tone2)
    # Crescent shade bottom-right
    h.clip(body, lambda: h.tint(ell(cx + radius * 0.42, by + ry * 0.45, radius * 1.05, ry * 1.05, 24), "blue", 0.34))
    # Specular paper dot upper-left
    h.dot(cx - radius * 0.36, by - ry * 0.36, max(1.7, radius * 0.14), "paper", 1.0, knock=True)
    h.outline(rng, body, "blue", w=1.35, tone=0.95, amp=0.12)

    # Optional cross-body loot duffel strap for thief
    if satchel_ink:
        strap = [(cx - radius * 0.62, by - ry * 0.30), (cx + radius * 0.42, by + ry * 0.56)]
        h.line(rng, strap, satchel_ink, w=2.4, tone=0.95, amp=0.08)
        pouch = ell(cx + radius * 0.50, by + ry * 0.44, radius * 0.26, radius * 0.22, 12)
        h.fill(pouch, satchel_ink, 0.92)
        h.outline(rng, pouch, "blue", w=0.95, tone=0.95, amp=0.06)

    # Crisp visor / eyes facing camera-front (+i/+j)
    ex = cx + radius * 0.12
    ey = by - ry * 0.08
    for dx in (-radius * 0.22, radius * 0.20):
        eye_white = ell(ex + dx, ey, radius * 0.16, radius * 0.20, 12)
        h.fill(eye_white, "paper", 1.0)
        h.outline(rng, eye_white, "blue", w=0.9, tone=0.95, amp=0.04)
        h.dot(ex + dx + 1.0, ey + 0.5, max(1.2, radius * 0.08), "black", 1.0, knock=True)

    # Role cap / beanie (slight tilt during stride)
    cap_y = by - ry * 0.72
    cap_shift = 0.8 * stride
    cap = loop(
        [
            (cx - radius * 0.72 + cap_shift, cap_y + 3.0),
            (cx - radius * 0.55 + cap_shift, cap_y - radius * 0.42),
            (cx + cap_shift, cap_y - radius * 0.52),
            (cx + radius * 0.58 + cap_shift, cap_y - radius * 0.38),
            (cx + radius * 0.72 + cap_shift, cap_y + 3.0),
        ],
        2,
    )
    h.fill(cap, cap_ink, cap_tone)
    h.clip(cap, lambda: h.tint([(cx, cap_y - radius), (cx + radius, cap_y - radius), (cx + radius, cap_y + 8), (cx, cap_y + 8)], "blue", 0.22))
    h.outline(rng, cap, "blue", w=1.15, tone=0.95, amp=0.10)

    if badge_ink:
        badge = ell(cx - radius * 0.34, by + ry * 0.26, radius * 0.20, radius * 0.16, 10)
        h.fill(badge, badge_ink, 0.95)
        h.outline(rng, badge, "blue", w=0.85, tone=0.95, amp=0.04)


def render_actor_sprite(role: str, scale: int = 2) -> Tuple[cairo.ImageSurface, Dict]:
    w_world, h_world = 72, 92
    ox, oy = 36.0, 72.0
    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)
    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"actor-{role}", transparent_sprite=True)
    rng = stream_of("actor", role)

    if role == "player":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.82, cap_ink="sun", cap_tone=0.95, satchel_ink="sun")
    elif role == "player_walk1":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.82, cap_ink="sun", cap_tone=0.95, stride=1, satchel_ink="sun")
    elif role == "player_walk2":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.82, cap_ink="sun", cap_tone=0.95, stride=-1, satchel_ink="sun")
    elif role == "player_crouch":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.85, cap_ink="sun", cap_tone=0.95, crouch=True, satchel_ink="sun")
    elif role == "player_crouch_walk1":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.85, cap_ink="sun", cap_tone=0.95, crouch=True, stride=1, satchel_ink="sun")
    elif role == "player_crouch_walk2":
        _draw_bot_body(h, rng, ox, oy, 16.5, "teal", 0.85, cap_ink="sun", cap_tone=0.95, crouch=True, stride=-1, satchel_ink="sun")
    elif role == "guard":
        _draw_bot_body(h, rng, ox, oy, 18.0, "blue", 0.92, "black", 0.25, cap_ink="coral", cap_tone=0.92, badge_ink="sun")
    elif role == "guard_walk1":
        _draw_bot_body(h, rng, ox, oy, 18.0, "blue", 0.92, "black", 0.25, cap_ink="coral", cap_tone=0.92, badge_ink="sun", stride=1)
    elif role == "guard_walk2":
        _draw_bot_body(h, rng, ox, oy, 18.0, "blue", 0.92, "black", 0.25, cap_ink="coral", cap_tone=0.92, badge_ink="sun", stride=-1)
    elif role == "staff":
        _draw_bot_body(h, rng, ox, oy, 16.0, "sun", 0.82, "coral", 0.14, cap_ink="teal", cap_tone=0.78)
    elif role == "staff_walk1":
        _draw_bot_body(h, rng, ox, oy, 16.0, "sun", 0.82, "coral", 0.14, cap_ink="teal", cap_tone=0.78, stride=1)
    elif role == "staff_walk2":
        _draw_bot_body(h, rng, ox, oy, 16.0, "sun", 0.82, "coral", 0.14, cap_ink="teal", cap_tone=0.78, stride=-1)
    elif role == "cctv":
        shadow_at(h, ox + 3.0, oy + 1.0, 8.0, 4.0, 0.18)
        h.line(rng, [(ox, oy), (ox, oy - 52.0)], "blue", w=2.2, tone=0.95, amp=0.05)
        box_at(h, rng, -0.22, -0.22, 0.44, 0.44, 1.55, 0.24, ["blue", 0.85], shadow=False, lw=1.0)
        h.dot(ox + 6.0, oy - 54.0, 3.2, "sun", 1.0, knock=True)
        h.outline(rng, circ(ox + 6.0, oy - 54.0, 3.2, 12), "blue", w=0.85, tone=0.95, amp=0.04)
        h.dot(ox - 5.0, oy - 58.0, 2.0, "coral", 1.0, knock=True)
    elif role == "bubble_alert":
        box = [(ox - 11, oy - 48), (ox + 11, oy - 48), (ox + 11, oy - 26), (ox + 3, oy - 26), (ox, oy - 20), (ox - 3, oy - 26), (ox - 11, oy - 26)]
        h.fill(box, "coral", 0.95)
        h.outline(rng, box, "blue", w=1.25, tone=0.95, amp=0.08)
        h.line(rng, [(ox, oy - 43), (ox, oy - 34)], "paper", w=2.8, amp=0.0)
        h.dot(ox, oy - 30.0, 1.7, "paper", 1.0, knock=True)
    elif role == "bubble_question":
        box = [(ox - 11, oy - 48), (ox + 11, oy - 48), (ox + 11, oy - 26), (ox + 3, oy - 26), (ox, oy - 20), (ox - 3, oy - 26), (ox - 11, oy - 26)]
        h.fill(box, "sun", 0.95)
        h.outline(rng, box, "blue", w=1.25, tone=0.95, amp=0.08)
        arc = arc_pts(ox, oy - 39.5, 3.6, 3.4, math.pi, math.tau + 0.4, 10)
        h.line(rng, arc + [(ox, oy - 34.5)], "blue", w=2.2, amp=0.0)
        h.dot(ox, oy - 30.2, 1.5, "blue", 1.0, knock=True)

    meta = {
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": ox / w_world,
        "pivotY": 1.0 - (oy / h_world),
        "pixelsPerUnit": 64.0 * scale,
    }
    return surf, meta


def write_ui_dither_tile(path: str, ink: str, tone: float, bg_paper: bool = True) -> None:
    """Writes a seamless 8x8 Bayer riso tile PNG for UI Toolkit progress bars & highlights."""
    surf, ctx = make_surface(8, 8, fill_paper=bg_paper)
    level = max(1, min(64, int(round(tone * 64))))
    tile = dither_tile_surface(ink, level, 255)
    ctx.set_operator(cairo.OPERATOR_MULTIPLY if bg_paper else cairo.OPERATOR_OVER)
    ctx.set_source_surface(tile, 0, 0)
    ctx.paint()
    surf.write_to_png(path)
