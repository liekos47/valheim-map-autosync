"""Draws the MapAutoSync icon, 256x256 PNG: a round Valheim-style world that looks drawn with
colour pens, and the word SYNC inside a refresh symbol.

    python make_icon.py <out.png> [world seed] [font file for the word]

The font is meant to be Valheim's own heading font (Norse Bold), which is not in this repository;
without it a bold system font is used."""
import math
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 1024  # drawn at 4x and scaled down
OUT = sys.argv[1]
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 5
FONT = sys.argv[3] if len(sys.argv) > 3 else None
rng = np.random.default_rng(SEED)


def noise(octaves, base):
    """Smooth fractal noise in 0..1, S x S."""
    total = np.zeros((S, S), dtype=np.float32)
    amp, norm, cells = 1.0, 0.0, base
    for _ in range(octaves):
        small = Image.fromarray((rng.random((cells, cells)) * 255).astype(np.uint8))
        total += amp * np.asarray(small.resize((S, S), Image.BICUBIC), dtype=np.float32) / 255.0
        norm += amp
        amp *= 0.5
        cells *= 2
    return total / norm


yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
cx = cy = S * 0.47          # the world sits a little up and left, leaving room for the badge
R = S * 0.445
dx, dy = (xx - cx) / R, (yy - cy) / R
r = np.sqrt(dx * dx + dy * dy)

land_n = noise(5, 5)
biome_n = noise(4, 4)
mount_n = noise(4, 7)
# Land: plenty in the middle, breaking up into islands, open sea at the rim.
land = (land_n + 0.20 * (1 - r) - 0.10 * np.clip((r - 0.86) / 0.14, 0, 1)) > 0.535

C = {
    "deep": (38, 62, 96), "sea": (52, 84, 124), "shallow": (86, 126, 160),
    "meadows": (140, 168, 84), "forest": (74, 112, 66), "swamp": (128, 104, 76),
    "mountain": (236, 238, 240), "plains": (214, 186, 104), "mist": (92, 86, 108),
    "ash": (176, 60, 44), "north": (226, 236, 244),
}
img = np.zeros((S, S, 3), dtype=np.float32)
t = np.clip((r - 0.35) / 0.6, 0, 1)[..., None]  # sea deepens smoothly towards the rim
img[:] = np.array(C["sea"], dtype=np.float32) * (1 - t) + np.array(C["deep"], dtype=np.float32) * t
img = img * (0.86 + 0.28 * noise(3, 6)[..., None])  # gentle variation in the water

rb = r + 0.22 * (biome_n - 0.5)  # ring boundaries wobble
biome = np.full((S, S), "meadows", dtype=object)
biome[rb > 0.24] = "forest"
biome[(rb > 0.36) & (biome_n > 0.52)] = "swamp"
biome[rb > 0.50] = "plains"
biome[(rb > 0.50) & (biome_n < 0.44)] = "forest"
biome[rb > 0.68] = "mist"
biome[(mount_n > 0.63) & (rb > 0.18) & (rb < 0.80)] = "mountain"
dyb = dy + 0.30 * (mount_n - 0.5)
biome[(dyb > 0.62) & (rb > 0.55)] = "ash"     # far south
biome[(dyb < -0.62) & (rb > 0.55)] = "north"  # far north
for name, colour in C.items():
    if name in ("deep", "sea", "shallow"):
        continue
    img[land & (biome == name)] = colour

# Lighter water hugging the coasts.
land_img = Image.fromarray((land * 255).astype(np.uint8))
near = np.asarray(land_img.filter(ImageFilter.GaussianBlur(S * 0.012)), dtype=np.float32) / 255.0
mix = np.clip(near * 1.6, 0, 1)[..., None]
img = np.where(land[..., None], img, img * (1 - mix * 0.7) + np.array(C["shallow"], dtype=np.float32) * mix * 0.7)

# A little round-world shading: lit from the upper left, darker towards the rim.
light = 1.06 - 0.22 * np.clip(r, 0, 1) ** 2.2 + 0.06 * (-(dx + dy) / 2)
img *= np.clip(light, 0.7, 1.2)[..., None]

# --- Drawn with colour pens -------------------------------------------------------------------
PAPER = np.array((250, 246, 236), dtype=np.float32)
INK = np.array((34, 32, 44), dtype=np.float32)


def strokes(angle, width, length):
    """Pen strokes: streaky noise in 0..1, lines `width` px thick running at `angle` degrees."""
    big = int(S * 1.5)
    small = Image.fromarray((rng.random((big // width, max(2, big // length))) * 255).astype(np.uint8))
    tex = small.resize((big, big), Image.BICUBIC).rotate(angle, Image.BICUBIC)
    o = (big - S) // 2
    return np.asarray(tex.crop((o, o, o + S, o + S)), dtype=np.float32) / 255.0


def borders(a):
    e = np.zeros((S, S), dtype=bool)
    e[:-1] |= a[:-1] != a[1:]
    e[:, :-1] |= a[:, :-1] != a[:, 1:]
    return e


def line(mask, px):
    im = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(px)).filter(ImageFilter.GaussianBlur(1.6))
    return np.asarray(im, dtype=np.float32) / 255.0


# Colouring in: strokes one way on land and another on water, paper showing where they miss.
hatch = np.where(land, strokes(38, 10, 140), strokes(-16, 12, 240))
gaps = np.clip((0.40 - hatch) * 2.4, 0, 0.62)[..., None]
img = img * (1 - gaps) + PAPER * gaps
img *= (1 - 0.16 * np.clip((hatch - 0.62) * 2.6, 0, 1))[..., None]  # darker where strokes overlap

# Ink lines: a firm one along the coasts, a lighter one between biomes, pressed unevenly.
ids = np.zeros((S, S), dtype=np.int16)
for k, name in enumerate(sorted(set(C) - {"deep", "sea", "shallow"}), 1):
    ids[land & (biome == name)] = k
ink = np.maximum(0.92 * line(borders(land), 11), 0.55 * line(borders(ids), 7)) * (0.7 + 0.3 * noise(3, 10))
img = img * (1 - ink[..., None]) + INK * ink[..., None]
img = np.clip(img, 0, 255)

# The world and an ink ring round it, so it reads on light and dark pages alike.
rim = 0.05
edge = np.clip((r - 1.0) * R / 2.0 + 0.5, 0, 1)[..., None]
rgba = np.dstack([img * (1 - edge) + INK * edge, np.clip((1.0 + rim - r) * R / 2.0, 0, 1) * 255])

# An unsteady hand: everything drawn so far shifts by a pixel or two, smoothly.
wob_x = np.rint(xx + (noise(2, 6) - 0.5) * S * 0.014).clip(0, S - 1).astype(np.intp)
wob_y = np.rint(yy + (noise(2, 6) - 0.5) * S * 0.014).clip(0, S - 1).astype(np.intp)
canvas = Image.fromarray(rgba[wob_y, wob_x].astype(np.uint8), "RGBA")

# Bottom right: the word SYNC inside a refresh symbol, white with a dark outline and no backing
# disc. The two arrows leave gaps at the left and right, where the word comes closest to them.
bx = by = S * 0.742
ar = S * 0.242           # outer radius of the arrow circle
aw = int(S * 0.040)      # stroke width
mask = Image.new("L", (S, S), 0)
d = ImageDraw.Draw(mask)
for start in (205, 25):  # PIL angles run clockwise from 3 o'clock
    end = start + 118
    d.arc([bx - ar, by - ar, bx + ar, by + ar], start, end, fill=255, width=aw)
    a = math.radians(start)
    mid = ar - aw / 2                                   # centre line of the stroke
    d.ellipse([bx + mid * math.cos(a) - aw / 2, by + mid * math.sin(a) - aw / 2,
               bx + mid * math.cos(a) + aw / 2, by + mid * math.sin(a) + aw / 2], fill=255)
    e = math.radians(end)
    px, py = bx + mid * math.cos(e), by + mid * math.sin(e)
    tx, ty = -math.sin(e), math.cos(e)                  # direction of travel (clockwise)
    nx, ny = math.cos(e), math.sin(e)                   # outwards
    h, w = aw * 1.9, aw * 1.5
    d.polygon([(px + tx * h, py + ty * h), (px + nx * w, py + ny * w), (px - nx * w, py - ny * w)], fill=255)

inner = (ar - aw) * 0.97  # the word's corners must stay inside the arrows
for size in range(int(S * 0.20), int(S * 0.05), -2):
    font = None
    for name in ([FONT] if FONT else []) + ["seguibl.ttf", "ariblk.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"]:
        try:
            font = ImageFont.truetype(name, size)
            break
        except OSError:
            pass
    left, top, right, bottom = d.textbbox((0, 0), "SYNC", font=font)
    if math.hypot((right - left) / 2, (bottom - top) / 2) <= inner:
        break
d.text((bx - (right - left) / 2 - left, by - (bottom - top) / 2 - top), "SYNC", font=font, fill=255)
mask = Image.fromarray(np.asarray(mask)[wob_y, wob_x])

outline = mask.filter(ImageFilter.MaxFilter(2 * int(S * 0.016) + 1)).filter(ImageFilter.GaussianBlur(1.5))
canvas.alpha_composite(Image.merge("RGBA", [Image.new("L", (S, S), v) for v in (16, 18, 24)] + [outline]))
canvas.alpha_composite(Image.merge("RGBA", [Image.new("L", (S, S), v) for v in (252, 249, 240)] + [mask]))

canvas.resize((256, 256), Image.LANCZOS).save(OUT)
print("wrote", OUT)
