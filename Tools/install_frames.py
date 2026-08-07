"""
Normalize rendered character frames and install them into CharactersAnim.

All frames share ONE fixed crop box (camera + root are fixed across every render),
so directions/frames stay perfectly aligned with no jitter. The box is centered
horizontally on the body axis, sized to contain every pose (union alpha bbox),
padded to a 3:4 canvas, and anchored so feet sit near the bottom.
"""
import os, sys, glob
from PIL import Image

RENDER = sys.argv[1]
DEST   = sys.argv[2]
OUT_W  = int(sys.argv[3]) if len(sys.argv) > 3 else 240
OUT_H  = int(round(OUT_W * 4 / 3))
ASPECT = 3/4  # w/h

frames = sorted(glob.glob(os.path.join(RENDER, "*.png")))
frames = [f for f in frames if not os.path.basename(f).startswith(("prev_", "_"))]
if not frames:
    print("NO_FRAMES"); sys.exit(1)

# render dimensions + body center (world x=0 -> horizontal center)
w0, h0 = Image.open(frames[0]).size
cx = w0 / 2.0

# ---- pass 1: union alpha bbox, symmetric about body center ----
top_min, bot_max, half_w = h0, 0, 0
bad = 0
for f in frames:
    bb = Image.open(f).convert("RGBA").split()[3].getbbox()
    if not bb:
        bad += 1; continue
    l, t, r, b = bb
    top_min = min(top_min, t); bot_max = max(bot_max, b)
    half_w = max(half_w, cx - l, r - cx)

content_h = bot_max - top_min
content_w = 2 * half_w

# ---- frame on BODY HEIGHT; width follows 3:4 aspect (prop may clip slightly) ----
ch = content_h / 0.86               # ~9% headroom top, ~5% below feet
cw = ch * ASPECT
cw = max(cw, content_w * 0.9)       # don't clip the prop too hard
ch = max(ch, cw / ASPECT)           # keep >= aspect height

# crop rectangle in render space: centered at cx, feet anchored near bottom
crop_x0 = cx - cw / 2.0
crop_x1 = cx + cw / 2.0
crop_y1 = bot_max + ch * 0.05      # small margin below feet
crop_y0 = crop_y1 - ch
box = (int(round(crop_x0)), int(round(crop_y0)), int(round(crop_x1)), int(round(crop_y1)))
print(f"union top={top_min} bottom={bot_max} half_w={half_w:.0f} bad={bad}")
print(f"crop box={box}  canvas={cw:.0f}x{ch:.0f}  -> out {OUT_W}x{OUT_H}")

# ---- pass 2: crop every frame with the same box, resize, install ----
n = 0
for f in frames:
    im = Image.open(f).convert("RGBA")
    canvas = Image.new("RGBA", (box[2]-box[0], box[3]-box[1]), (0,0,0,0))
    # paste source region (handling out-of-bounds via crop which pads transparent)
    region = im.crop(box)
    canvas.paste(region, (0,0))
    canvas = canvas.resize((OUT_W, OUT_H), Image.LANCZOS)
    dst = os.path.join(DEST, os.path.basename(f))
    canvas.save(dst)
    n += 1
print(f"INSTALLED {n} frames -> {DEST}")
