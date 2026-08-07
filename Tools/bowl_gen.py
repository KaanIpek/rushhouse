"""
Bowl-cuisine dish generator (PIL).
Top-down grain bowls: a drawn ceramic bowl + rice base + the existing painterly
ingredient sprites arranged in sections. Sidesteps the folded-3D-shape problem —
these composite cleanly and sit at the painterly food's quality bar.

  py bowl_gen.py --foods <Art/Foods> --out-dishes <Art/FinalDishes> --out-foods <Art/Foods>
"""
import os, argparse, math, random
from PIL import Image, ImageDraw, ImageFilter

ap = argparse.ArgumentParser()
ap.add_argument("--foods", required=True)
ap.add_argument("--out-dishes", required=True)
ap.add_argument("--out-foods", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args()
R = args.res

DISHES = {
    "ricebowl": ["patty"],
    "greenbowl": ["patty", "lettuce"],
    "gardenbowl": ["lettuce", "tomato", "onion"],
    "cheddarbowl": ["patty", "cheese"],
    "fiesta": ["patty", "cheese", "tomato", "onion"],
}
SRC = {"patty": "pattyCooked", "lettuce": "lettuce", "tomato": "tomato", "cheese": "cheese", "onion": "onion"}


def load(n):
    return Image.open(os.path.join(args.foods, n + ".png")).convert("RGBA")


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def paste(im, sp, x, y, w, rot=0):
    s = sp.copy()
    s.thumbnail((w, w), Image.LANCZOS)
    if rot:
        s = s.rotate(rot, expand=True, resample=Image.BICUBIC)
    im.alpha_composite(s, (int(x - s.width / 2), int(y - s.height / 2)))


def bowl_base(rice=True):
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy = R // 2, int(R * 0.52)
    rx, ry = 142, 98
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - 122, cy + 42, cx + 122, cy + 112), fill=(0, 0, 0, 120))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(11)))
    for y in range(cy, cy + 92):                       # bowl side wall
        t = (y - cy) / 92
        col = lerp((240, 240, 246), (146, 148, 158), t)
        hw = int(rx * (1 - 0.34 * t))
        d.line([(cx - hw, y), (cx + hw, y)], fill=(*col, 255))
    d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(246, 246, 252))
    d.ellipse((cx - rx + 10, cy - ry + 8, cx + rx - 10, cy + ry - 6), fill=(212, 214, 224))
    iw, ih = rx - 22, ry - 16
    d.ellipse((cx - iw, cy - ih + 6, cx + iw, cy + ih + 8), fill=(206, 194, 168))  # warm rice base
    if rice:
        random.seed(11)
        for _ in range(900):                           # dense rice fill
            a = random.uniform(0, math.tau)
            rr = random.uniform(0, 1) ** 0.5
            gx = cx + math.cos(a) * iw * 0.95 * rr
            gy = cy + 4 + math.sin(a) * ih * 0.9 * rr
            L = random.randint(5, 8)
            col = random.choice([(244, 238, 222), (226, 218, 196), (252, 248, 238), (236, 228, 208)])
            d.ellipse((gx - 2, gy - L // 2, gx + 3, gy + L // 2), fill=(*col, 255))
            if random.random() < 0.25:                  # subtle grain shadow for texture
                d.point((gx + 3, gy + L // 2), fill=(150, 140, 118, 160))
    return im, (cx, cy, iw, ih)


def build_dish(name):
    im, (cx, cy, iw, ih) = bowl_base(True)
    fills = DISHES[name]
    random.seed(hash(name) & 0xffff)
    n = len(fills)
    for i, ing in enumerate(fills):
        sp = load(SRC[ing])
        ang = -math.pi / 2 + (i + 0.5) / n * math.tau     # section centre angle
        sx = cx + math.cos(ang) * iw * 0.46
        sy = cy + 4 + math.sin(ang) * ih * 0.44
        w = {"patty": 92, "lettuce": 96, "tomato": 66, "cheese": 68, "onion": 74}[ing]
        cnt = {"patty": 2, "lettuce": 2, "tomato": 3, "cheese": 2, "onion": 1}[ing]
        for k in range(cnt):
            jx = sx + random.randint(-18, 18)
            jy = sy + random.randint(-14, 14)
            paste(im, sp, jx, jy, w, random.randint(-22, 22))
    return im


def build_rice_ingredient():
    # small scoop of rice for the counter/held view (a compact mound)
    im, (cx, cy, iw, ih) = bowl_base(True)
    return im


def main():
    os.makedirs(args.out_dishes, exist_ok=True)
    os.makedirs(args.out_foods, exist_ok=True)
    for n in DISHES:
        build_dish(n).save(os.path.join(args.out_dishes, n + ".png"))
        print("dish", n)
    build_rice_ingredient().save(os.path.join(args.out_foods, "rice.png"))
    print("BOWLGEN_DONE")


main()
