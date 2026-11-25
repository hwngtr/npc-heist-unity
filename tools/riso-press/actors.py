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
    stride_phase: float | None = None,
    vest_ink: str | None = None,
    visor_brim: bool = False,
    cap_emblem_ink: str | None = None,
    radio_antenna: bool = False,
) -> None:
    ry = radius * (0.76 if crouch else 1.0)
    if stride_phase is not None:
        theta = stride_phase * math.tau
        swing = math.sin(theta)
        abs_swing = abs(swing)
        bob = -0.6 - 2.2 * abs_swing
        sh_scale = 1.0 - 0.10 * abs_swing
        left_dx = -radius * 0.36 + (3.6 * swing)
        left_dy = -0.5 - 2.6 * max(0.0, swing) + 0.9 * max(0.0, -swing)
        right_dx = radius * 0.36 - (3.6 * swing)
        right_dy = -0.5 - 2.6 * max(0.0, -swing) + 0.9 * max(0.0, swing)
        cap_shift = 0.95 * swing
    else:
        bob = -2.2 if stride != 0 else 0.0
        sh_scale = 0.92 if stride != 0 else 1.0
        left_dx = -radius * 0.36 + (3.2 * stride)
        left_dy = -0.5 + (-2.4 if stride > 0 else 0.8 if stride < 0 else 0.0)
        right_dx = radius * 0.36 - (3.2 * stride)
        right_dy = -0.5 + (-2.4 if stride < 0 else 0.8 if stride > 0 else 0.0)
        cap_shift = 0.8 * stride
    by = cy - ry * 0.92 + bob

    # Tight under-foot contact AO + grounded cast shadow
    shadow_at(h, cx + 0.5, cy + 0.5, radius * 0.90 * sh_scale, radius * 0.38 * sh_scale, 0.36)
    shadow_at(h, cx + 3.0, cy + 1.8, radius * 1.05 * sh_scale, radius * 0.44 * sh_scale, 0.18)

    # Optional shoulder radio antenna (behind head)
    if radio_antenna:
        ant_x = cx - radius * 0.72 + cap_shift * 0.4
        ant_y0 = by - ry * 0.10
        ant_y1 = by - ry * 1.38
        h.line(rng, [(ant_x, ant_y0), (ant_x - 1.5, ant_y1)], "black", w=1.8, tone=0.95, amp=0.05)
        h.dot(ant_x - 1.5, ant_y1, 2.2, "coral", 1.0, knock=True)
        h.outline(rng, circ(ant_x - 1.5, ant_y1, 2.2, 10), "blue", w=0.85, tone=0.95, amp=0.04)

    # Animated stride feet
    for dx, dy in ((left_dx, left_dy), (right_dx, right_dy)):
        foot = ell(cx + dx, cy + dy, radius * 0.25, radius * 0.15, 12)
        h.fill(foot, "black", 0.92)
        h.outline(rng, foot, "blue", w=1.0, tone=0.95, amp=0.05)

    # Round bot silhouette
    body = ell(cx, by, radius, ry, 28)
    h.fill(body, ink, tone)
    if ink2 and tone2 > 0:
        h.tint(body, ink2, tone2)
    # Optional tactical utility vest over lower torso
    if vest_ink:
        vest = [
            (cx - radius * 0.96, by + ry * 0.02),
            (cx + radius * 0.96, by + ry * 0.02),
            (cx + radius * 0.96, by + ry * 1.05),
            (cx - radius * 0.96, by + ry * 1.05),
        ]
        h.clip(body, lambda: h.tint(vest, vest_ink, 0.68))
        belt = [(cx - radius * 0.88, by + ry * 0.16), (cx + radius * 0.88, by + ry * 0.16)]
        h.line(rng, belt, "paper", w=1.6, tone=0.95, amp=0.06)
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

    if visor_brim:
        brim = loop(
            [
                (cx - radius * 0.12 + cap_shift, cap_y + 1.5),
                (cx + radius * 0.92 + cap_shift, cap_y + 0.5),
                (cx + radius * 0.85 + cap_shift, cap_y + 4.8),
                (cx - radius * 0.05 + cap_shift, cap_y + 4.2),
            ],
            2,
        )
        h.fill(brim, "blue", 0.90)
        h.outline(rng, brim, "blue", w=0.95, tone=0.95, amp=0.06)

    if cap_emblem_ink:
        emb = ell(cx + radius * 0.12 + cap_shift, cap_y - radius * 0.18, radius * 0.16, radius * 0.12, 10)
        h.fill(emb, cap_emblem_ink, 0.95)
        h.outline(rng, emb, "blue", w=0.75, tone=0.95, amp=0.04)

    if badge_ink:
        badge = ell(cx - radius * 0.34, by + ry * 0.28, radius * 0.22, radius * 0.17, 10)
        h.fill(badge, badge_ink, 0.95)
        h.outline(rng, badge, "blue", w=0.85, tone=0.95, amp=0.04)


_BOT_PRESETS = {
    "player": dict(radius=16.5, ink="teal", tone=0.82, cap_ink="sun", cap_tone=0.95, satchel_ink="sun"),
    "player_crouch": dict(radius=16.5, ink="teal", tone=0.85, cap_ink="sun", cap_tone=0.95, crouch=True, satchel_ink="sun"),
    "guard": dict(radius=18.0, ink="blue", tone=0.92, ink2="black", tone2=0.25, cap_ink="coral", cap_tone=0.92, badge_ink="sun"),
    "guard2": dict(
        radius=18.0,
        ink="teal",
        tone=0.88,
        ink2="black",
        tone2=0.22,
        vest_ink="black",
        cap_ink="sun",
        cap_tone=0.95,
        visor_brim=True,
        cap_emblem_ink="coral",
        badge_ink="coral",
        radio_antenna=True,
    ),
    "staff": dict(radius=16.0, ink="sun", tone=0.82, ink2="coral", tone2=0.14, cap_ink="teal", cap_tone=0.78),
}


def render_actor_sprite(role: str, scale: int = 2) -> Tuple[cairo.ImageSurface, Dict]:
    w_world, h_world = 72, 92
    ox, oy = 36.0, 72.0
    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)

    base_role = role
    stride = 0
    stride_phase = None
    seed_role = role
    for prefix in sorted(_BOT_PRESETS.keys(), key=len, reverse=True):
        if role == prefix:
            base_role = prefix
            break
        if role == f"{prefix}_walk1":
            base_role, stride = prefix, 1
            break
        if role == f"{prefix}_walk2":
            base_role, stride = prefix, -1
            break
        if role.startswith(f"{prefix}_walk_"):
            frame_idx = int(role.split("_")[-1])
            base_role = prefix
            stride_phase = frame_idx / 6.0
            seed_role = prefix
            break

    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"actor-{seed_role}", transparent_sprite=True)
    rng = stream_of("actor", seed_role)

    if base_role in _BOT_PRESETS:
        _draw_bot_body(h, rng, ox, oy, stride=stride, stride_phase=stride_phase, **_BOT_PRESETS[base_role])
    elif role == "cctv":
        shadow_at(h, ox + 2.0, oy - 14.0, 9.0, 4.5, 0.18)
        # Sleek Cairo Riso wall-mount security turret + dome housing
        h.line(rng, [(ox, oy - 14.0), (ox, oy - 48.0)], "blue", w=2.6, tone=0.95, amp=0.05)
        mount = ell(ox, oy - 49.0, 7.5, 4.2, 14)
        h.fill(mount, "blue", 0.85)
        h.outline(rng, mount, "blue", w=1.0, tone=0.95, amp=0.06)
        box_at(h, rng, -0.26, -0.22, 0.52, 0.44, 1.38, 0.28, ["paper", 1.0], top_col=["teal", 0.28], shadow=False, lw=1.15)
        lens_hood = ell(ox + 8.5, oy - 47.0, 5.2, 4.2, 14)
        h.fill(lens_hood, "black", 0.90)
        h.outline(rng, lens_hood, "blue", w=1.0, tone=0.95, amp=0.05)
        h.dot(ox + 9.2, oy - 47.0, 3.0, "sun", 1.0, knock=True)
        h.outline(rng, circ(ox + 9.2, oy - 47.0, 3.0, 12), "blue", w=0.85, tone=0.95, amp=0.04)
        h.dot(ox - 5.5, oy - 56.0, 2.3, "coral", 1.0, knock=True)
        h.outline(rng, circ(ox - 5.5, oy - 56.0, 2.3, 10), "blue", w=0.75, tone=0.95, amp=0.04)
    elif role == "cctv_mount":
        # Ground-anchored security stanchion base rising exactly +0.56 world units (36 world px = 72 px @ 128 PPU)
        shadow_at(h, ox + 0.5, oy + 0.8, 8.2, 4.0, 0.34)
        shadow_at(h, ox + 2.6, oy + 1.8, 8.8, 4.2, 0.16)
        base_plate = ell(ox, oy, 7.2, 3.6, 16)
        h.fill(base_plate, "blue", 0.88)
        h.outline(rng, base_plate, "blue", w=1.15, tone=0.96, amp=0.05)
        h.line(rng, [(ox, oy - 1.0), (ox, oy - 36.0)], "blue", w=2.6, tone=0.95, amp=0.04)
        collar = ell(ox, oy - 36.0, 4.8, 2.6, 12)
        h.fill(collar, "teal", 0.85)
        h.outline(rng, collar, "blue", w=0.95, tone=0.95, amp=0.04)
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


def render_prop_sprite(name: str, scale: int = 2) -> Tuple[cairo.ImageSurface, Dict]:
    """Renders 128-PPU Cairo Riso Press interactive world props with grounded floor pivots."""
    w_world, h_world = 96, 108
    ox, oy = 48.0, 72.0
    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)
    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"prop-{name}", dp=0.5, transparent_sprite=True)
    rng = stream_of("prop", name)

    if name == "prop_cctv_panel":
        box_at(h, rng, -0.45, -0.35, 0.90, 0.70, 0.0, 1.32, ["blue", 0.85, "black", 0.12], top_col=["teal", 0.35], shadow=True, lw=1.35)
        # Cream recessed breaker door on front face
        door = [h.p(0.45, -0.24, 0.20), h.p(0.45, 0.24, 0.20), h.p(0.45, 0.24, 1.12), h.p(0.45, -0.24, 1.12)]
        h.fill(door, "paper", 1.0)
        h.tint(door, "teal", 0.16)
        h.outline(rng, door, "blue", w=1.0, tone=0.95, amp=0.05)
        # Sun danger plaque + Coral high-voltage bolt
        c_pt = h.p(0.45, 0.0, 0.72)
        plaque = ell(c_pt[0], c_pt[1], 7.0, 8.5, 12)
        h.fill(plaque, "sun", 0.96)
        h.outline(rng, plaque, "blue", w=0.9, tone=0.95, amp=0.04)
        bolt = [(c_pt[0] + 1.5, c_pt[1] - 5.0), (c_pt[0] - 2.5, c_pt[1] + 0.5), (c_pt[0] + 0.5, c_pt[1] + 0.5), (c_pt[0] - 1.5, c_pt[1] + 5.5)]
        h.line(rng, bolt, "coral", w=2.2, tone=0.95, amp=0.03)
        # Active red LED on top rim
        led = h.p(0.15, 0.18, 1.34)
        h.dot(led[0], led[1], 2.8, "coral", 1.0, knock=True)
        h.outline(rng, circ(led[0], led[1], 2.8, 10), "blue", w=0.85, tone=0.95, amp=0.04)
    elif name == "prop_detagger":
        box_at(h, rng, -0.52, -0.52, 1.04, 1.04, 0.0, 0.42, ["blue", 0.78], top_col=["paper", 1.0], shadow=True, lw=1.35)
        deck = h.tile(-0.42, -0.42, 0.84, 0.84, 0.43)
        h.fill(deck, "teal", 0.25)
        h.outline(rng, deck, "blue", w=0.95, tone=0.95, amp=0.05)
        tc = h.p(0.0, 0.0, 0.43)
        for rx, ry, ink, t in ((13.5, 7.2, "teal", 0.85), (8.2, 4.4, "blue", 0.85)):
            ring = ell(tc[0], tc[1], rx, ry, 20)
            h.outline(rng, ring, ink, w=1.4, tone=t, amp=0.05)
        h.dot(tc[0], tc[1], 3.4, "sun", 1.0, knock=True)
        h.outline(rng, circ(tc[0], tc[1], 3.4, 12), "blue", w=0.95, tone=0.95, amp=0.04)
        h.dot(tc[0] + 10.5, tc[1] + 4.5, 2.3, "coral", 1.0, knock=True)
    elif name == "prop_decoy_coin":
        shadow_at(h, ox + 0.8, oy + 1.5, 16.0, 8.0, 0.34)
        cy = oy - 4.0
        rim = ell(ox, cy + 4.2, 17.5, 11.5, 22)
        h.fill(rim, "coral", 0.88)
        h.outline(rng, rim, "blue", w=1.2, tone=0.95, amp=0.06)
        face = ell(ox, cy, 17.5, 11.5, 22)
        h.fill(face, "sun", 0.98)
        h.outline(rng, face, "blue", w=1.35, tone=0.95, amp=0.06)
        inner = ell(ox, cy, 12.2, 7.8, 18)
        h.outline(rng, inner, "blue", w=0.95, tone=0.80, amp=0.04)
        star = star_pts(ox, cy, 5.8, 2.8, 5)
        h.fill(star, "coral", 0.88)
        h.outline(rng, star, "blue", w=0.8, tone=0.95, amp=0.03)
        h.dot(ox - 7.5, cy - 4.0, 2.3, "paper", 1.0, knock=True)

    meta = {
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": ox / w_world,
        "pivotY": 1.0 - (oy / h_world),
        "pixelsPerUnit": 64.0 * scale,
    }
    return surf, meta


def render_ui_badge_sprite(name: str) -> cairo.ImageSurface:
    """Renders 64x64 Cairo Riso Press HUD medallions for squad roles and luxury objectives."""
    s = 64
    cx, cy = 32.0, 32.0
    surf, ctx = make_surface(s, s, fill_paper=False)
    h = RisoHand(ctx, ox=cx, oy=cy, seed=f"badge-{name}", dp=0.5, transparent_sprite=True)
    rng = stream_of("badge", name)

    ring_ink = {
        "role_patrol": ("teal", 0.86),
        "role_pursuer": ("coral", 0.94),
        "role_interceptor": ("sun", 0.95),
        "role_sabotage": ("coral", 0.90),
        "role_sweeper": ("blue", 0.90),
        "icon_spirits_trophy": ("sun", 0.95),
    }.get(name, ("blue", 0.88))

    outer = circ(cx, cy, 25.5, 24)
    h.fill(outer, ring_ink[0], ring_ink[1])
    h.clip(outer, lambda: h.tint(ell(cx + 8, cy + 9, 24, 24, 20), "blue", 0.24))
    h.outline(rng, outer, "blue", w=1.8, tone=0.95, amp=0.06)

    inner = circ(cx, cy, 18.5, 22)
    h.fill(inner, "paper", 1.0)
    h.outline(rng, inner, "blue", w=1.1, tone=0.85, amp=0.04)

    if name == "role_patrol":
        # Patrol beat shield & waypoint trail dots
        shield = [(cx - 9, cy - 9), (cx + 9, cy - 9), (cx + 9, cy + 1), (cx, cy + 11), (cx - 9, cy + 1)]
        h.fill(shield, "teal", 0.82)
        h.outline(rng, shield, "blue", w=1.15, tone=0.95, amp=0.04)
        h.dot(cx, cy - 1.5, 3.0, "paper", 1.0, knock=True)
    elif name == "role_pursuer":
        # High-alert crosshair & coral center
        h.outline(rng, circ(cx, cy, 10.5, 18), "coral", w=2.0, tone=0.95, amp=0.04)
        for seg in (((cx - 14, cy), (cx + 14, cy)), ((cx, cy - 14), (cx, cy + 14))):
            h.line(rng, seg, "blue", w=1.6, tone=0.95, amp=0.03)
        h.dot(cx, cy, 4.2, "coral", 1.0, knock=True)
    elif name == "role_interceptor":
        # EAS exit barrier diamond + coral hazard stripes
        gate = [(cx - 11, cy - 6), (cx + 11, cy - 6), (cx + 11, cy + 6), (cx - 11, cy + 6)]
        h.fill(gate, "sun", 0.92)
        h.outline(rng, gate, "blue", w=1.15, tone=0.95, amp=0.04)
        for sx in (-6.0, 0.0, 6.0):
            h.line(rng, [(cx + sx - 2, cy - 5), (cx + sx + 2, cy + 5)], "coral", w=2.4, tone=0.95, amp=0.02)
    elif name == "role_sabotage":
        # High-voltage sabotage lightning bolt
        bolt = [(cx + 2, cy - 12), (cx - 6, cy + 1), (cx + 1, cy + 1), (cx - 3, cy + 12), (cx + 7, cy - 2), (cx, cy - 2)]
        h.fill(bolt, "coral", 0.95)
        h.outline(rng, bolt, "blue", w=1.1, tone=0.95, amp=0.04)
    elif name == "role_sweeper":
        # Sector sweep radar wedge + blip
        wedge = [(cx, cy)] + arc_pts(cx, cy, 12.5, 12.5, -2.55, -0.55, 10)
        h.fill(wedge, "teal", 0.85)
        h.outline(rng, wedge, "blue", w=1.0, tone=0.95, amp=0.04)
        h.dot(cx, cy, 3.0, "blue", 1.0, knock=True)
        h.dot(cx + 5.5, cy - 7.0, 2.5, "coral", 1.0, knock=True)
    elif name == "icon_spirits_trophy":
        # Luxury single-malt vitrine bottle emblem
        body = [(cx - 6.5, cy - 2), (cx + 6.5, cy - 2), (cx + 6.5, cy + 12), (cx - 6.5, cy + 12)]
        h.fill(body, "sun", 0.95)
        h.outline(rng, body, "blue", w=1.15, tone=0.95, amp=0.04)
        neck = [(cx - 2.6, cy - 12), (cx + 2.6, cy - 12), (cx + 3.2, cy - 2), (cx - 3.2, cy - 2)]
        h.fill(neck, "coral", 0.92)
        h.outline(rng, neck, "blue", w=1.05, tone=0.95, amp=0.03)
        lbl = [(cx - 4.2, cy + 2), (cx + 4.2, cy + 2), (cx + 4.2, cy + 8), (cx - 4.2, cy + 8)]
        h.fill(lbl, "paper", 1.0)
        h.outline(rng, lbl, "blue", w=0.8, tone=0.95, amp=0.02)

    return surf


def render_cctv_head_sprite(dir_idx: int, scale: int = 2) -> Tuple[cairo.ImageSurface, Dict]:
    """
    Renders one of 8 directional Cairo Riso camera turret heads (dir_idx 0..7 in 45 deg CCW Unity screen steps).
    Pivot sits dead-center at the gimbal mount so the head pivots smoothly left-to-right with its V-cone.
    """
    w_world, h_world = 48, 48
    ox, oy = 24.0, 24.0
    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)

    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"cctv-head-{dir_idx}", transparent_sprite=True)
    rng = stream_of("cctv_head", str(dir_idx))

    # Unity screen angle: 0=+X right, +90=+Y up; Cairo +Y is down -> uy = -sin(theta)
    theta = (dir_idx % 8) * (math.tau / 8.0)
    ux = math.cos(theta)
    uy = -math.sin(theta)
    px = -uy
    py = ux

    def pt(fwd: float, side: float, z_off: float = 0.0) -> Tuple[float, float]:
        return (ox + ux * fwd + px * side, oy + uy * fwd + py * side - z_off)

    # Swivel turret housing + paper casing + teal top screen + midnight-blue outline
    body = [pt(-7.5, -5.0), pt(6.0, -4.5), pt(6.0, 4.5), pt(-7.5, 5.0)]
    h.fill(body, "paper", 1.0)
    h.tint(body, "teal", 0.30)
    h.clip(body, lambda: h.tint([pt(-8.0, 0.0), pt(7.0, 0.0), pt(7.0, 6.0), pt(-8.0, 6.0)], "blue", 0.24))
    h.outline(rng, body, "blue", w=1.25, tone=0.95, amp=0.05)

    # Dark optical lens barrel pointing along aim direction
    hood = [pt(4.5, -3.8), pt(10.5, -3.2), pt(10.5, 3.2), pt(4.5, 3.8)]
    h.fill(hood, "black", 0.90)
    h.outline(rng, hood, "blue", w=1.05, tone=0.95, amp=0.04)

    # Glowing Sun-yellow glass lens on barrel tip
    lx, ly = pt(10.8, 0.0)
    lens = circ(lx, ly, 3.1, 12)
    h.fill(lens, "sun", 0.98)
    h.outline(rng, lens, "blue", w=0.90, tone=0.95, amp=0.03)
    h.dot(lx - ux * 0.8 - 0.6, ly - uy * 0.8 - 0.6, 1.2, "paper", 1.0, knock=True)

    # Coral active recording LED on top-rear housing
    rx, ry = pt(-3.8, 0.0, 2.2)
    led = circ(rx, ry, 2.1, 10)
    h.fill(led, "coral", 0.98)
    h.outline(rng, led, "blue", w=0.80, tone=0.95, amp=0.03)

    meta = {
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": 0.5,
        "pivotY": 0.5,
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
