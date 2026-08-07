"""Normalize + install 3D food renders into Art/Foods, Art/FinalDishes, Art/Carry.
Each item is alpha-cropped, square-padded and centered (keeps .meta files)."""
import os, sys, glob
from PIL import Image

SRC_ING, SRC_DISH, ART = sys.argv[1], sys.argv[2], sys.argv[3]
SIZE = int(sys.argv[4]) if len(sys.argv) > 4 else 256

def norm(path, margin=0.08):
    im = Image.open(path).convert("RGBA")
    bb = im.split()[3].getbbox()
    if bb: im = im.crop(bb)
    w, h = im.size
    s = max(w, h); pad = int(s * margin)
    canvas = Image.new("RGBA", (s + 2*pad, s + 2*pad), (0, 0, 0, 0))
    canvas.paste(im, (pad + (s - w)//2, pad + (s - h)//2))
    return canvas.resize((SIZE, SIZE), Image.LANCZOS)

def install(src, folders):
    n = 0
    for p in sorted(glob.glob(os.path.join(src, "*.png"))):
        name = os.path.basename(p)
        if name.startswith("_"): continue
        img = norm(p)
        for f in folders:
            os.makedirs(f, exist_ok=True)
            img.save(os.path.join(f, name))
        n += 1
    return n

foods = os.path.join(ART, "Foods")
dishes = os.path.join(ART, "FinalDishes")
carry = os.path.join(ART, "Carry")
nd = install(SRC_DISH, [dishes, carry])      # dishes first
ni = install(SRC_ING, [foods, carry])        # ingredients (ingredient 'cheese' wins in Carry)
print(f"FOOD INSTALLED  dishes={nd}  ingredients={ni}")
