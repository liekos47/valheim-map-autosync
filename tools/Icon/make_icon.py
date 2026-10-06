"""Draws the AutoSyncMap icon: a round Valheim-style world with a refresh badge. 256x256 PNG."""
import math
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024  # drawn at 4x and scaled down
OUT = sys.argv[1]
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 7
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

# Shallow water hugging the coasts, and a darker coastline.
land_img = Image.fromarray((land * 255).astype(np.uint8))
near = np.asarray(land_img.filter(ImageFilter.GaussianBlur(S * 0.012)), dtype=np.float32) / 255.0
water = ~land
mix = np.clip(near * 1.6, 0, 1)[..., None]
img = np.where(water[..., None], img * (1 - mix * 0.7) + np.array(C["shallow"], dtype=np.float32) * mix * 0.7, img)
edge = np.asarray(land_img.filter(ImageFilter.GaussianBlur(S * 0.004)), dtype=np.float32) / 255.0
coast = land & (edge < 0.78)
img[coast] *= 0.72

# Round-world shading: lit from the upper left, darker towards the rim.
light = 1.10 - 0.34 * np.clip(r, 0, 1) ** 2.2 + 0.10 * (-(dx + dy) / 2)
img *= np.clip(light, 0.55, 1.25)[..., None]
img = np.clip(img, 0, 255)

alpha = np.clip((1.0 - r) * R / 2.0, 0, 1)  # soft edge, about 2 px at 4x
world = Image.fromarray(np.dstack([img, alpha * 255]).astype(np.uint8), "RGBA")

canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(canvas)
# A dark ring behind the world so it reads on light and dark pages alike.
rim = S * 0.022
d.ellipse([cx - R - rim, cy - R - rim, cx + R + rim, cy + R + rim], fill=(24, 30, 44, 255))
canvas.alpha_composite(world)

# Refresh arrows, bottom right: white with a dark outline, no backing disc.
bx = by = S * 0.765
br = S * 0.215
ar = br * 0.62           # radius of the arrow circle
aw = int(br * 0.22)      # stroke width
mask = Image.new("L", (S, S), 0)
d = ImageDraw.Draw(mask)
for start in (200, 20):  # two arcs, each ending in an arrowhead (PIL angles run clockwise)
    end = start + 118
    d.arc([bx - ar, by - ar, bx + ar, by + ar], start, end, fill=255, width=aw)
    a = math.radians(start)
    d.ellipse([bx + (ar - aw / 2) * math.cos(a) - aw / 2, by + (ar - aw / 2) * math.sin(a) - aw / 2,
               bx + (ar - aw / 2) * math.cos(a) + aw / 2, by + (ar - aw / 2) * math.sin(a) + aw / 2], fill=255)
    e = math.radians(end)
    mid = ar - aw / 2                                   # centre line of the stroke
    px, py = bx + mid * math.cos(e), by + mid * math.sin(e)
    tx, ty = -math.sin(e), math.cos(e)                  # direction of travel (clockwise)
    nx, ny = math.cos(e), math.sin(e)                   # outwards
    h, w = aw * 1.9, aw * 1.45
    d.polygon([(px + tx * h, py + ty * h), (px + nx * w, py + ny * w), (px - nx * w, py - ny * w)], fill=255)
outline = mask.filter(ImageFilter.MaxFilter(2 * int(S * 0.016) + 1)).filter(ImageFilter.GaussianBlur(1.5))
canvas.alpha_composite(Image.merge("RGBA", [Image.new("L", (S, S), v) for v in (16, 18, 24)] + [outline]))
canvas.alpha_composite(Image.merge("RGBA", [Image.new("L", (S, S), 255)] * 3 + [mask]))

canvas.resize((256, 256), Image.LANCZOS).save(OUT)
print("wrote", OUT)
