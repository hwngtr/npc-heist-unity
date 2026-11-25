#!/usr/bin/env python3
"""
SpriteCook Technique 20 — 4-View Orthographic to Horizontal Slice Strip Slicer.
Reads a 2x2 orthographic reference sheet:
  Top-Left:     Top view (facing down / +y)
  Top-Right:    Side view (facing left / -x)
  Bottom-Left:  Front view
  Bottom-Right: Back view
Intersects the 4 silhouettes per height row and writes a horizontal slice strip PNG
(ground slice z=0 on the left, top slice z=N-1 on the right) for Unity's SpriteStack2D.
"""
import argparse
import os
from PIL import Image


def is_bg(rgba, tol=28):
    r, g, b, a = rgba
    if a < 128:
        return True
    # Solid white or near-white background
    return r >= 255 - tol and g >= 255 - tol and b >= 255 - tol


def crop_opaque(img):
    px = img.load()
    w, h = img.size
    min_x, min_y, max_x, max_y = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if not is_bg(px[x, y]):
                if x < min_x: min_x = x
                if y < min_y: min_y = y
                if x > max_x: max_x = x
                if y > max_y: max_y = y
    if max_x < min_x:
        return img.crop((0, 0, 1, 1))
    return img.crop((min_x, min_y, max_x + 1, max_y + 1))


def slice_ortho_2x2(src_path, dst_path, slice_w=24, slice_h=28, slice_count=14):
    im = Image.open(src_path).convert("RGBA")
    W, H = im.size
    hw, hh = W // 2, H // 2

    q_top = crop_opaque(im.crop((0, 0, hw, hh))).resize((slice_w, slice_h), Image.NEAREST)
    q_side = crop_opaque(im.crop((hw, 0, W, hh))).resize((slice_h, slice_count), Image.NEAREST)
    q_front = crop_opaque(im.crop((0, hh, hw, H))).resize((slice_w, slice_count), Image.NEAREST)
    q_back = crop_opaque(im.crop((hw, hh, W, H))).resize((slice_w, slice_count), Image.NEAREST)

    top_px = q_top.load()
    side_px = q_side.load()
    front_px = q_front.load()
    back_px = q_back.load()

    # Precompute solid masks
    top_solid = [[not is_bg(top_px[x, y]) for x in range(slice_w)] for y in range(slice_h)]
    side_solid = [[not is_bg(side_px[y, r]) for y in range(slice_h)] for r in range(slice_count)]
    front_solid = [[not is_bg(front_px[x, r]) for x in range(slice_w)] for r in range(slice_count)]
    back_solid = [[not is_bg(back_px[x, r]) for x in range(slice_w)] for r in range(slice_count)]

    # Build 3D voxel occupancy: voxel[z][y][x] where z=0 is ground (bottom row of side/front)
    occ = [[[False] * slice_w for _ in range(slice_h)] for _ in range(slice_count)]
    for z in range(slice_count):
        r = slice_count - 1 - z  # bottom row -> z=0
        for y in range(slice_h):
            # Side view points left: x_side=0 is front (y = slice_h - 1 in top view)
            sy = slice_h - 1 - y
            if not side_solid[r][sy]:
                continue
            for x in range(slice_w):
                if top_solid[y][x] and (front_solid[r][x] or back_solid[r][slice_w - 1 - x]):
                    occ[z][y][x] = True

    out = Image.new("RGBA", (slice_w * slice_count, slice_h), (0, 0, 0, 0))
    out_px = out.load()

    for z in range(slice_count):
        r = slice_count - 1 - z
        for y in range(slice_h):
            sy = slice_h - 1 - y
            for x in range(slice_w):
                if not occ[z][y][x]:
                    continue

                # Top surface of column takes top view color; outer 2px rim takes side/front/back color
                is_top = (z == slice_count - 1) or (not occ[z + 1][y][x])
                if is_top:
                    col = top_px[x, y]
                elif y >= slice_h - 2 and front_solid[r][x]:
                    col = front_px[x, r]
                elif y <= 1 and back_solid[r][slice_w - 1 - x]:
                    col = back_px[slice_w - 1 - x, r]
                elif (x <= 1 or x >= slice_w - 2) and side_solid[r][sy]:
                    col = side_px[sy, r]
                else:
                    col = top_px[x, y]

                out_px[z * slice_w + x, y] = (col[0], col[1], col[2], 255)

    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    out.save(dst_path)
    print(f"Sliced {src_path} -> {dst_path} ({slice_w}x{slice_h} x {slice_count} slices = {out.size[0]}x{out.size[1]})")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("src")
    p.add_argument("dst")
    p.add_argument("--w", type=int, default=24)
    p.add_argument("--h", type=int, default=28)
    p.add_argument("--slices", type=int, default=14)
    args = p.parse_args()
    slice_ortho_2x2(args.src, args.dst, args.w, args.h, args.slices)
