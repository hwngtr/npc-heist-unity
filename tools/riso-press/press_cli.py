#!/usr/bin/env python3
"""
CLI for the Frischemarkt 2D Riso Press framework.

Commands:
  sheet         Render a visual contact sheet of all 33 fixtures and actors.
  kind <name>   Render a close-up preview (ne + nw) of a single kind.
  map           Render the complete 12-room supermarket + close-up entrance view.
  export-unity  Bake 2x RGBA sprites + rooms + UI Bayer tiles into Unity Assets.
"""

from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
from typing import Any, Dict, List, Tuple

import cairo

from actors import render_actor_sprite, write_ui_dither_tile
from catalog import (
    LOOT_BEARING_KINDS,
    MARKET_FIXTURES,
    MARKET_KINDS,
    MARKET_ROOMS,
    make_empty_kind,
    make_open_vitrine_kind,
)
from kinds import Placer, draw_thing, screen_box
from press import INK, PAPER, RisoHand, iso, make_surface, stream_of
from world import PITCH, draw_shell, room_bounds, room_edges, room_origin

ROOT = Path(__file__).resolve().parents[2]
PREVIEW_DIR = ROOT / "tools" / "riso-press" / "previews"
UNITY_SPRITES_DIR = ROOT / "Assets" / "HeistSim" / "Market2D" / "Sprites"


def render_kind_sprite(
    kind_name: str,
    face: str = "ne",
    scale: int = 2,
    kind_override: Dict[str, Any] | None = None,
) -> Tuple[cairo.ImageSurface, Dict[str, Any]]:
    base_kind = MARKET_KINDS[kind_name]
    kind = kind_override if kind_override is not None else base_kind
    item = {"id": f"{kind_name}_{face}", "kind": kind_name, "at": {"i": 0.0, "j": 0.0, "z": 0.0, "face": face}}
    # Always compute screen_box from base_kind so stocked/open/empty share exact dimensions and pivot!
    box = screen_box(base_kind, item)
    w_world = max(32, box["x1"] - box["x0"])
    h_world = max(32, box["y1"] - box["y0"])
    ox = float(-box["x0"])
    oy = float(-box["y0"])

    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)
    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"kind-{kind_name}-{face}", transparent_sprite=True)
    rng = stream_of("kind", kind_name, face)
    draw_thing(h, rng, kind, item)

    meta = {
        "kind": kind_name,
        "face": face,
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": ox / w_world,
        "pivotY": 1.0 - (oy / h_world),
        "pixelsPerUnit": 64.0 * scale,
    }
    return surf, meta


def render_room_sprite(
    rm: Dict[str, Any],
    edges: Dict[str, str],
    scale: int = 2,
    draw_low_walls: bool = False,
) -> Tuple[cairo.ImageSurface, Dict[str, Any]]:
    b = room_bounds(rm)
    w_world = int(math.ceil(b["x1"] - b["x0"]))
    h_world = int(math.ceil(b["y1"] - b["y0"]))
    ox = float(-b["x0"])
    oy = float(-b["y0"])

    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)
    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"room-{rm['id']}", transparent_sprite=False)
    rng = stream_of("room", rm["id"])
    draw_shell(h, rng, rm, edges=edges, draw_low_walls=draw_low_walls)

    meta = {
        "room": rm["id"],
        "at": rm["at"],
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": ox / w_world,
        "pivotY": 1.0 - (oy / h_world),
        "pixelsPerUnit": 64.0 * scale,
    }
    return surf, meta


def render_low_wall_segment_sprite(side: str, length: float, scale: int = 2) -> Tuple[cairo.ImageSurface, Dict[str, Any]]:
    """Renders one depth-sortable low divider wall segment (along NE or NW edge)."""
    from world import LOW_H, box_at
    t = 0.07
    pts = []
    if side == "ne":
        for i in (0.0, length):
            for j in (-t, t):
                for z in (0.0, LOW_H):
                    pts.append(iso(i, j, z))
    else:
        for i in (-t, t):
            for j in (0.0, length):
                for z in (0.0, LOW_H):
                    pts.append(iso(i, j, z))
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    pad = 14
    x0 = int(math.floor(min(xs) - pad))
    y0 = int(math.floor(min(ys) - pad))
    x1 = int(math.ceil(max(xs) + pad))
    y1 = int(math.ceil(max(ys) + pad))
    w_world = max(16, x1 - x0)
    h_world = max(16, y1 - y0)
    ox = float(-x0)
    oy = float(-y0)

    surf, ctx = make_surface(w_world * scale, h_world * scale, fill_paper=False)
    ctx.scale(scale, scale)
    h = RisoHand(ctx, ox=ox, oy=oy, seed=f"lowwall-{side}-{length:.2f}", transparent_sprite=True)
    rng = stream_of("lowwall", side, length)
    col = ["blue", 0.24]
    if side == "ne":
        box_at(h, rng, 0.0, -t, length, 2 * t, 0.0, LOW_H, col, shadow=False, lw=0.95, top_col=["paper", 1.0])
    else:
        box_at(h, rng, -t, 0.0, 2 * t, length, 0.0, LOW_H, col, shadow=False, lw=0.95, top_col=["paper", 1.0])

    meta = {
        "side": side,
        "length": length,
        "width": w_world * scale,
        "height": h_world * scale,
        "pivotX": ox / w_world,
        "pivotY": 1.0 - (oy / h_world),
        "pixelsPerUnit": 64.0 * scale,
    }
    return surf, meta


def cmd_sheet() -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    names = sorted(MARKET_KINDS.keys())
    cols = 6
    cell_w, cell_h = 250, 210
    rows = int(math.ceil(len(names) / cols)) + 1  # extra row for actors
    surf, ctx = make_surface(cols * cell_w, rows * cell_h, fill_paper=True)

    ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
    ctx.set_font_size(13)

    for idx, name in enumerate(names):
        col = idx % cols
        row = idx // cols
        x0, y0 = col * cell_w, row * cell_h
        # Cell border
        ctx.set_source_rgba(52 / 255, 74 / 255, 128 / 255, 0.25)
        ctx.set_line_width(1.0)
        ctx.rectangle(x0 + 4, y0 + 4, cell_w - 8, cell_h - 8)
        ctx.stroke()

        sp, meta = render_kind_sprite(name, "ne", scale=1)
        scale_fit = min((cell_w - 28) / max(1, sp.get_width()), (cell_h - 42) / max(1, sp.get_height()), 1.0)
        dw = sp.get_width() * scale_fit
        dh = sp.get_height() * scale_fit
        dx = x0 + (cell_w - dw) * 0.5
        dy = y0 + (cell_h - 24 - dh) * 0.5 + 4
        ctx.save()
        ctx.translate(dx, dy)
        ctx.scale(scale_fit, scale_fit)
        ctx.set_source_surface(sp, 0, 0)
        ctx.paint()
        ctx.restore()

        ctx.set_source_rgb(52 / 255, 74 / 255, 128 / 255)
        ctx.move_to(x0 + 12, y0 + cell_h - 12)
        ctx.show_text(name)

    # Bottom row: actors & markers
    actor_roles = ["player", "player_crouch", "guard", "staff", "cctv", "loot_rf"]
    base_y = (rows - 1) * cell_h
    for col, role in enumerate(actor_roles):
        x0 = col * cell_w
        ctx.set_source_rgba(52 / 255, 74 / 255, 128 / 255, 0.25)
        ctx.set_line_width(1.0)
        ctx.rectangle(x0 + 4, base_y + 4, cell_w - 8, cell_h - 8)
        ctx.stroke()

        sp, _ = render_actor_sprite(role, scale=2)
        dx = x0 + (cell_w - sp.get_width()) * 0.5
        dy = base_y + (cell_h - 24 - sp.get_height()) * 0.5 + 4
        ctx.set_source_surface(sp, dx, dy)
        ctx.paint()

        ctx.set_source_rgb(52 / 255, 74 / 255, 128 / 255)
        ctx.move_to(x0 + 12, base_y + cell_h - 12)
        ctx.show_text(f"actor:{role}")

    out_path = PREVIEW_DIR / "contact-sheet.png"
    surf.write_to_png(str(out_path))
    print(f"Wrote {out_path} ({surf.get_width()}x{surf.get_height()})")


def cmd_kind(name: str) -> None:
    if name not in MARKET_KINDS:
        raise SystemExit(f"Unknown kind: {name}")
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sp_ne, _ = render_kind_sprite(name, "ne", scale=2)
    sp_nw, _ = render_kind_sprite(name, "nw", scale=2)
    pad = 24
    w = sp_ne.get_width() + sp_nw.get_width() + pad * 3
    h = max(sp_ne.get_height(), sp_nw.get_height()) + pad * 2 + 24
    surf, ctx = make_surface(w, h, fill_paper=True)
    ctx.set_source_surface(sp_ne, pad, pad)
    ctx.paint()
    ctx.set_source_surface(sp_nw, pad * 2 + sp_ne.get_width(), pad)
    ctx.paint()
    ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
    ctx.set_font_size(14)
    ctx.set_source_rgb(52 / 255, 74 / 255, 128 / 255)
    ctx.move_to(pad, h - 10)
    ctx.show_text(f"{name} (ne)")
    ctx.move_to(pad * 2 + sp_ne.get_width(), h - 10)
    ctx.show_text(f"{name} (nw)")
    out_path = PREVIEW_DIR / f"kind-{name}.png"
    surf.write_to_png(str(out_path))
    print(f"Wrote {out_path}")


def cmd_map() -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    edges_map = room_edges(MARKET_ROOMS)
    rooms_by_id = {rm["id"]: rm for rm in MARKET_ROOMS}

    # Compute world extents in iso pixels
    xs, ys = [], []
    for rm in MARKET_ROOMS:
        rx, ry = room_origin(rm["at"])
        b = room_bounds(rm)
        xs.extend([rx + b["x0"], rx + b["x1"]])
        ys.extend([ry + b["y0"], ry + b["y1"]])
    min_x, max_x = min(xs) - 40, max(xs) + 40
    min_y, max_y = min(ys) - 40, max(ys) + 40
    w = int(math.ceil(max_x - min_x))
    h = int(math.ceil(max_y - min_y))

    surf, ctx = make_surface(w, h, fill_paper=True)
    ox_base, oy_base = -min_x, -min_y

    # 1. Draw rooms back-to-front by (at[0] + at[1])
    sorted_rooms = sorted(MARKET_ROOMS, key=lambda r: (r["at"][0] + r["at"][1], r["at"][0]))
    for rm in sorted_rooms:
        rx, ry = room_origin(rm["at"])
        hand = RisoHand(ctx, ox=ox_base + rx, oy=oy_base + ry, seed=f"room-{rm['id']}")
        rng = stream_of("room", rm["id"])
        draw_shell(hand, rng, rm, edges=edges_map[rm["id"]], draw_low_walls=True)

    # 2. Draw all 78 fixtures sorted by global isometric depth (gi + gj)
    def fixture_depth(fx: Dict[str, Any]) -> float:
        rm = rooms_by_id[fx["room"]]
        kind = MARKET_KINDS[fx["kind"]]
        gi = rm["at"][0] * PITCH + fx["at"]["i"]
        gj = rm["at"][1] * PITCH + fx["at"]["j"]
        w_sz, d_sz = Placer(fx["at"], kind["size"]).size(kind["size"][0], kind["size"][1])
        return gi + w_sz * 0.5 + gj + d_sz * 0.5

    for fx in sorted(MARKET_FIXTURES, key=fixture_depth):
        rm = rooms_by_id[fx["room"]]
        kind = MARKET_KINDS[fx["kind"]]
        rx, ry = room_origin(rm["at"])
        hand = RisoHand(ctx, ox=ox_base + rx, oy=oy_base + ry, seed=f"fx-{fx['id']}")
        rng = stream_of("fx", fx["id"])
        draw_thing(hand, rng, kind, fx)

    # 3. Draw actors at spawn positions
    for role, gi, gj in (("player", 2.8, 3.2), ("guard", 7.0, 4.5), ("staff", 20.5, 4.5)):
        ax, ay = iso(gi, gj, 0.0)
        sp, meta = render_actor_sprite(role, scale=1)
        px = meta["pivotX"] * sp.get_width()
        py = (1.0 - meta["pivotY"]) * sp.get_height()
        ctx.set_source_surface(sp, ox_base + ax - px, oy_base + ay - py)
        ctx.paint()

    out_full = PREVIEW_DIR / "market-overview.png"
    surf.write_to_png(str(out_full))
    print(f"Wrote {out_full} ({w}x{h})")

    # Also save a crisp 1200x760 gameplay-zoom crop centered around entrance / breakfast / dairy
    crop_w, crop_h = 1200, 760
    cx, cy = iso(9.5, 6.5, 0.0)
    src_x = int(ox_base + cx - crop_w * 0.5)
    src_y = int(oy_base + cy - crop_h * 0.5)
    crop_surf, crop_ctx = make_surface(crop_w, crop_h, fill_paper=True)
    crop_ctx.set_source_surface(surf, -src_x, -src_y)
    crop_ctx.paint()
    out_crop = PREVIEW_DIR / "market-closeup.png"
    crop_surf.write_to_png(str(out_crop))
    print(f"Wrote {out_crop} ({crop_w}x{crop_h})")


def cmd_export_unity(scale: int = 2) -> None:
    fixtures_dir = UNITY_SPRITES_DIR / "Fixtures"
    rooms_dir = UNITY_SPRITES_DIR / "Rooms"
    actors_dir = UNITY_SPRITES_DIR / "Actors"
    ui_dir = UNITY_SPRITES_DIR / "UI"
    for d in (fixtures_dir, rooms_dir, actors_dir, ui_dir):
        d.mkdir(parents=True, exist_ok=True)

    manifest: Dict[str, Any] = {
        "scale": scale,
        "tileWidthPx": 64 * scale,
        "tileHeightPx": 32 * scale,
        "pixelsPerUnit": 64 * scale,
        "fixtures": {},
        "rooms": {},
        "actors": {},
    }

    # Export every fixture kind in both 'ne' and 'nw' orientations
    for name in sorted(MARKET_KINDS.keys()):
        for face in ("ne", "nw"):
            key = f"{name}__{face}"
            sp, meta = render_kind_sprite(name, face, scale=scale)
            rel = f"Fixtures/{key}.png"
            sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
            meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
            manifest["fixtures"][key] = meta

    # Export empty/stolen variants for every loot-bearing fixture kind (exact same dimensions & pivot!)
    manifest["emptyFixtures"] = {}
    for name in sorted(LOOT_BEARING_KINDS):
        empty_kind = make_empty_kind(name)
        for face in ("ne", "nw"):
            key = f"{name}__{face}__empty"
            sp, meta = render_kind_sprite(name, face, scale=scale, kind_override=empty_kind)
            rel = f"Fixtures/{key}.png"
            sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
            meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
            manifest["emptyFixtures"][key] = meta

    # Export forced-open (unlocked, still stocked) spirits vitrine
    open_vitrine = make_open_vitrine_kind()
    for face in ("ne", "nw"):
        key = f"liquor-cabinet__{face}__open"
        sp, meta = render_kind_sprite("liquor-cabinet", face, scale=scale, kind_override=open_vitrine)
        rel = f"Fixtures/{key}.png"
        sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
        meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
        manifest["emptyFixtures"][key] = meta

    # Export all 12 cutaway rooms (floor + full outer walls; low dividers exported separately for true depth sorting)
    edges_map = room_edges(MARKET_ROOMS)
    for rm in MARKET_ROOMS:
        sp, meta = render_room_sprite(rm, edges_map[rm["id"]], scale=scale, draw_low_walls=False)
        rel = f"Rooms/{rm['id']}.png"
        sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
        meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
        manifest["rooms"][rm["id"]] = meta

    # Export depth-sortable low divider wall segments (left/right of the [3.55, 5.45] doorway gap)
    manifest["lowWalls"] = {}
    for side in ("ne", "nw"):
        key = f"lowwall_{side}"
        sp, meta = render_low_wall_segment_sprite(side, 3.55, scale=scale)
        rel = f"Rooms/{key}.png"
        sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
        meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
        manifest["lowWalls"][key] = meta

    # Remove legacy detached floating loot star sprites if present
    for stale in ("loot_glint.png", "loot_glint.png.meta", "loot_rf.png", "loot_rf.png.meta"):
        stale_path = actors_dir / stale
        if stale_path.exists():
            stale_path.unlink()

    # Export animated actors & world alert bubbles (no detached loot stars!)
    actor_roles = (
        "player", "player_walk1", "player_walk2",
        "player_crouch", "player_crouch_walk1", "player_crouch_walk2",
        "guard", "guard_walk1", "guard_walk2",
        "staff", "staff_walk1", "staff_walk2",
        "cctv", "bubble_alert", "bubble_question",
    )
    for role in actor_roles:
        sp, meta = render_actor_sprite(role, scale=scale)
        rel = f"Actors/{role}.png"
        sp.write_to_png(str(UNITY_SPRITES_DIR / rel))
        meta["path"] = f"Assets/HeistSim/Market2D/Sprites/{rel}"
        manifest["actors"][role] = meta

    # Export seamless 8x8 Bayer dither tiles for Unity UI Toolkit progress bars & highlights
    write_ui_dither_tile(str(ui_dir / "dither_coral.png"), "coral", 0.72, bg_paper=True)
    write_ui_dither_tile(str(ui_dir / "dither_sun.png"), "sun", 0.55, bg_paper=True)
    write_ui_dither_tile(str(ui_dir / "dither_teal.png"), "teal", 0.65, bg_paper=True)
    write_ui_dither_tile(str(ui_dir / "dither_blue.png"), "blue", 0.45, bg_paper=True)

    manifest_path = UNITY_SPRITES_DIR / "sprite-manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(
        f"Exported {len(manifest['fixtures'])} fixture sprites, "
        f"{len(manifest['rooms'])} rooms, {len(manifest['actors'])} actors -> {UNITY_SPRITES_DIR}"
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Frischemarkt 2D Riso Press CLI")
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("sheet", help="Render contact sheet of all kinds and actors")
    p_kind = sub.add_parser("kind", help="Render one kind close-up")
    p_kind.add_argument("name")
    sub.add_parser("map", help="Render full 12-room supermarket overview + close-up")
    p_exp = sub.add_parser("export-unity", help="Export 2x RGBA sprites and manifest into Unity")
    p_exp.add_argument("--scale", type=int, default=2)

    args = parser.parse_args()
    if args.cmd == "sheet":
        cmd_sheet()
    elif args.cmd == "kind":
        cmd_kind(args.name)
    elif args.cmd == "map":
        cmd_map()
    elif args.cmd == "export-unity":
        cmd_export_unity(scale=args.scale)


if __name__ == "__main__":
    main()
