#!/usr/bin/env python3
"""Phase 3 lunar terrain generator — a cohesive, seamless tile set for the
Space-Age total conversion.

Upgrades gen_tileset.py: seamless (edge-wrapped) tiles, a unified lunar palette,
and the full terrain vocabulary the mod needs — regolith variants, basalt,
ice deposits (the resource), crater floor, and crater-wall cliffs. Emits:

  * <out>/tiles/<name>.png          individual seamless tiles
  * <out>/lunar_sheet.png           packed sprite sheet
  * <out>/lunar_manifest.txt        index -> terrain type -> sheet xy
  * <out>/lunar_montage.png         labelled preview of the whole set
  * <out>/lunar_palette.png         the palette swatch

Terrain types map 1:1 onto tilesets/lunar.yaml and tilesets/mars.yaml, both of
which are registered in mod.yaml and load in-engine already; see PHASE3-NOTES.md.

Usage:
    python3 gen_lunar_terrain.py --palette moon --tile 48 --out ../artwork/lunar
    python3 gen_lunar_terrain.py --palette mars --tile 48 --out ../artwork/mars

Deps: numpy, Pillow.
"""
import argparse
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

# Unified palettes: (shadow, low, mid, high, accent) — accent = ice/mineral pop
PALETTES = {
    "moon": {
        "ramp": [(30, 28, 26), (58, 54, 50), (120, 112, 104), (182, 174, 163), (214, 208, 198)],
        "basalt": [(28, 28, 34), (52, 52, 62), (78, 80, 92)],
        "ice": [(120, 196, 232), (176, 226, 248), (224, 245, 255)],
    },
    "mars": {
        "ramp": [(46, 22, 16), (92, 46, 30), (150, 82, 52), (196, 120, 84), (222, 158, 120)],
        "basalt": [(34, 26, 28), (66, 44, 44), (96, 66, 62)],
        "ice": [(150, 208, 226), (196, 232, 244), (230, 248, 255)],
    },
}


def _periodic_octave(size, cells, rng):
    """One octave of value noise that is EXACTLY periodic over `size` (torus grid),
    so tiles wrap seamlessly with no diagonal seam. Bilinear + smoothstep."""
    grid = rng.random((cells, cells)).astype(np.float32)
    coord = np.linspace(0, cells, size, endpoint=False)
    i0 = np.floor(coord).astype(int) % cells
    i1 = (i0 + 1) % cells
    f = coord - np.floor(coord)
    f = f * f * (3 - 2 * f)                       # smoothstep
    y0, y1, fy = i0[:, None], i1[:, None], f[:, None]
    x0, x1, fx = i0[None, :], i1[None, :], f[None, :]
    g00 = grid[np.ix_(i0, i0)]; g01 = grid[np.ix_(i0, i1)]
    g10 = grid[np.ix_(i1, i0)]; g11 = grid[np.ix_(i1, i1)]
    top = g00 * (1 - fx) + g01 * fx
    bot = g10 * (1 - fx) + g11 * fx
    return top * (1 - fy) + bot * fy


def seamless_noise(size, scale, rng, octaves=(1.0, 0.5, 0.25)):
    """Multi-octave PERIODIC value noise — tiles seamlessly in all directions."""
    base_cells = max(2, size // max(2, scale))
    n = np.zeros((size, size), np.float32)
    w = 0.0
    for i, amp in enumerate(octaves):
        n += amp * _periodic_octave(size, base_cells * (2 ** i), rng)
        w += amp
    n /= w
    return (n - n.min()) / (np.ptp(n) + 1e-6)


def apply_ramp(n, ramp):
    ramp = [np.array(c, np.float32) for c in ramp]
    segs = len(ramp) - 1
    t = np.clip(n, 0, 0.999) * segs
    idx = t.astype(int)
    frac = (t - idx)[..., None]
    lo = np.stack([np.array(ramp)[i] for i in idx.ravel()]).reshape(n.shape + (3,))
    hi = np.stack([np.array(ramp)[min(i + 1, segs)] for i in idx.ravel()]).reshape(n.shape + (3,))
    return lo + (hi - lo) * frac


def regolith(size, pal, rng, bright=1.0):
    n = seamless_noise(size, size // 3, rng)
    img = apply_ramp(n * 0.9 + 0.05, pal["ramp"]) * bright
    # sparse pebbles
    p = seamless_noise(size, size // 8, rng)
    img[p > 0.93] = np.array(pal["ramp"][4]) * 0.9
    img[p < 0.05] = np.array(pal["ramp"][0]) * 1.1
    return np.clip(img, 0, 255).astype(np.uint8)


def basalt(size, pal, rng):
    n = seamless_noise(size, size // 4, rng)
    img = apply_ramp(n, pal["basalt"])
    crack = seamless_noise(size, size // 6, rng)
    img[np.abs(crack - 0.5) < 0.03] *= 0.5
    return np.clip(img, 0, 255).astype(np.uint8)


def ice(size, pal, rng):
    base = regolith(size, pal, rng, bright=0.85).astype(np.float32)
    n = seamless_noise(size, size // 5, rng)
    blob = np.clip((n - 0.45) * 3, 0, 1)[..., None]
    icecol = apply_ramp(seamless_noise(size, size // 7, rng), pal["ice"])
    return np.clip(base * (1 - blob) + icecol * blob, 0, 255).astype(np.uint8)


def crater_floor(size, pal, rng):
    base = regolith(size, pal, rng, bright=0.7).astype(np.float32)
    yy, xx = np.mgrid[0:size, 0:size]
    r = np.hypot(xx - size / 2, yy - size / 2) / (size / 2)
    shade = np.clip(0.5 + 0.5 * r, 0, 1)[..., None]  # dark centre
    return np.clip(base * shade, 0, 255).astype(np.uint8)


def crater_wall(size, pal, rng):
    """Cliff/impassable ring tile: bright sunlit rim, dark shadowed base."""
    base = regolith(size, pal, rng).astype(np.float32)
    grad = (np.linspace(1.25, 0.5, size))[:, None, None]  # top bright -> bottom dark
    edge = seamless_noise(size, size // 5, rng)[..., None]
    return np.clip(base * grad * (0.85 + 0.3 * edge), 0, 255).astype(np.uint8)


TILES = [
    ("Regolith", lambda s, p, r: regolith(s, p, r)),
    ("Regolith", lambda s, p, r: regolith(s, p, r, 1.08)),
    ("Regolith", lambda s, p, r: regolith(s, p, r, 0.92)),
    ("Basalt", basalt),
    ("IceDeposit", ice),
    ("CraterFloor", crater_floor),
    ("CraterWall", crater_wall),
]


def label(img, text):
    d = ImageDraw.Draw(img)
    d.rectangle([0, img.height - 14, img.width, img.height], fill=(0, 0, 0, 170))
    try:
        f = ImageFont.load_default()
    except Exception:
        f = None
    d.text((3, img.height - 13), text, fill=(230, 230, 230), font=f)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--palette", choices=list(PALETTES), default="moon")
    ap.add_argument("--tile", type=int, default=48)
    ap.add_argument("--seed", type=int, default=1969)
    ap.add_argument("--out", default="lunar_terrain")
    a = ap.parse_args()

    rng = np.random.default_rng(a.seed)
    pal = PALETTES[a.palette]
    t = a.tile
    os.makedirs(os.path.join(a.out, "tiles"), exist_ok=True)

    frames, manifest = [], ["index\tterrain\tsheet_xy"]
    cols = len(TILES)
    sheet = Image.new("RGBA", (cols * t, t), (0, 0, 0, 0))
    montage = Image.new("RGBA", (cols * (t + 6) + 6, t + 22), (18, 22, 28, 255))
    for i, (kind, fn) in enumerate(TILES):
        arr = fn(t, pal, rng)
        im = Image.fromarray(arr).convert("RGBA")
        im.save(os.path.join(a.out, "tiles", f"{i:02d}_{kind}.png"))
        sheet.paste(im, (i * t, 0))
        manifest.append(f"{i}\t{kind}\t({i*t},0)")
        cell = label(im.copy(), kind)
        montage.paste(cell, (6 + i * (t + 6), 6))
    sheet.save(os.path.join(a.out, "lunar_sheet.png"))
    montage.save(os.path.join(a.out, "lunar_montage.png"))
    with open(os.path.join(a.out, "lunar_manifest.txt"), "w") as f:
        f.write("\n".join(manifest) + "\n")

    # palette swatch
    allc = pal["ramp"] + pal["basalt"] + pal["ice"]
    sw = Image.new("RGB", (len(allc) * 40, 40))
    for i, c in enumerate(allc):
        for x in range(40):
            for y in range(40):
                sw.putpixel((i * 40 + x, y), tuple(c))
    sw.save(os.path.join(a.out, "lunar_palette.png"))

    print(f"[{a.palette}] wrote {len(TILES)} seamless tiles @ {t}px -> {a.out}/")
    print("  lunar_sheet.png, lunar_montage.png, lunar_manifest.txt, lunar_palette.png")


if __name__ == "__main__":
    main()
