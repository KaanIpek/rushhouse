"""
Painterly taco-theme dish generator (PIL).
Draws the tortilla/shell in a warm painterly style and composites the existing
painterly ingredient sprites (lettuce/cheese/tomato/onion) for the fillings, so
the dishes sit closer to the shipped painterly food than flat 3D primitives.

  py taco_dish_gen.py --foods <Art/Foods> --out <dir>
"""
import os, argparse, random, math
from PIL import Image, ImageDraw, ImageFilter

ap = argparse.ArgumentParser()
ap.add_argument("--foods", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args()
R = args.res


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def load(n):
    return Image.open(os.path.join(args.foods, n + ".png")).convert("RGBA")


def grad_mask(mask, top, bot):
    """Vertical-gradient RGBA filled inside an L mask (top->bottom over the mask bbox)."""
    lay = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    bb = mask.getbbox()
    if not bb:
        return lay
    y0, y1 = bb[1], bb[3]
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0)
        d.line([(0, y), (R, y)], fill=(*lerp(top, bot, t), 255))
    lay.putalpha(mask)
    return lay


def ground_shadow(im, x0=0.22, x1=0.78, y0=0.80, y1=0.92, a=95, blur=11):
    lay = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(lay).ellipse((R * x0, R * y0, R * x1, R * y1), fill=(0, 0, 0, a))
    im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(blur)))


def paste(im, sprite, cx, cy, w, bright=1.0, rot=0):
    s = sprite.copy()
    s.thumbnail((w, w), Image.LANCZOS)
    if rot:
        s = s.rotate(rot, expand=True, resample=Image.BICUBIC)
    if bright != 1.0:
        r, g, b, al = s.split()
        lut = [min(255, int(i * bright)) for i in range(256)]
        s = Image.merge("RGBA", (r.point(lut), g.point(lut), b.point(lut), al))
    im.alpha_composite(s, (int(cx - s.width / 2), int(cy - s.height / 2)))


def beef_crumble(im, cx, cy, rx, ry, n=90):
    d = ImageDraw.Draw(im, "RGBA")
    random.seed(7)
    tones = [(96, 56, 30), (120, 72, 40), (74, 42, 22), (140, 88, 52)]
    for _ in range(n):
        a = random.uniform(0, math.tau)
        rr = random.uniform(0, 1) ** 0.5
        x = cx + math.cos(a) * rx * rr
        y = cy + math.sin(a) * ry * rr
        r = random.randint(4, 8)
        c = random.choice(tones)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(*c, 255))
        d.ellipse((x - r, y - r, x - r + r, y - r + r), fill=(min(255, c[0] + 40), min(255, c[1] + 30), min(255, c[2] + 20), 150))


# ---- tortilla shell (a warm folded half-disc, opening up toward the viewer) ----
def shell(top=(238, 196, 120), bot=(150, 96, 44), rim=(250, 224, 168)):
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    bb = (int(R * 0.14), int(R * 0.30), int(R * 0.86), int(R * 0.86))
    # body = lower half-disc (flat top = the opening)
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).chord(bb, 0, 180, fill=255)
    body = grad_mask(m, top, bot)
    im.alpha_composite(body)
    d = ImageDraw.Draw(im, "RGBA")
    # curved bottom rim highlight
    d.arc((bb[0] + 4, bb[1] + 6, bb[2] - 4, bb[3] - 4), 12, 168, fill=(*rim, 150), width=6)
    # inner shadow just under the opening edge
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((bb[0] + 14, int(R * 0.30) - 6, bb[2] - 14, int(R * 0.30) + 40), fill=(70, 40, 18, 150))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(7)))
    # toasted char freckles
    random.seed(3)
    for _ in range(26):
        a = random.uniform(0.2, math.pi - 0.2)
        rr = random.uniform(0.35, 0.94)
        x = cx + math.cos(a) * (bb[2] - bb[0]) / 2 * rr
        y = int(R * 0.30) + math.sin(a) * (bb[3] - int(R * 0.30)) * rr * 0.9
        r = random.randint(2, 5)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(120, 74, 36, random.randint(70, 130)))
    return im, bb


def build_taco(fills):
    im, bb = shell()
    cx = R // 2
    openY = int(R * 0.31)
    # dark interior so filling reads as sitting inside
    inner = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(inner).ellipse((bb[0] + 24, openY - 4, bb[2] - 24, openY + 30), fill=(48, 26, 12, 210))
    im.alpha_composite(inner.filter(ImageFilter.GaussianBlur(4)))
    if "beef" in fills:
        beef_crumble(im, cx, openY + 12, (bb[2] - bb[0]) * 0.34, 20)
    if "cheese" in fills:
        ch = load("cheese")
        for dx in (-64, -18, 30, 74):
            paste(im, ch, cx + dx, openY + 6, 46, 1.0, rot=random.randint(-20, 20))
    if "tomato" in fills:
        tm = load("tomato")
        for dx in (-50, 4, 56):
            paste(im, tm, cx + dx, openY + 2, 40)
    if "onion" in fills:
        on = load("onion")
        paste(im, on, cx, openY + 4, 92, 1.05)
    # lettuce frills along the top, poking above the opening
    lt = load("lettuce")
    for dx in (-70, -30, 12, 54, 88):
        paste(im, lt, cx + dx, openY - 16, 62, 1.05, rot=random.randint(-25, 25))
    ground_shadow(im)
    return im


def build_dish(name):
    F = {
        "taco": ["beef", "lettuce", "cheese"],
        "carnita": ["beef", "cheese", "tomato", "onion"],
        "burrito": ["beef", "cheese"],
        "quesadilla": ["cheese"],
        "nachos": ["cheese", "tomato"],
    }[name]
    if name in ("taco", "carnita"):
        return build_taco(F)
    return build_taco(F)  # placeholder until other shapes are added


def main():
    os.makedirs(args.out, exist_ok=True)
    for n in ["taco", "carnita"]:
        build_dish(n).save(os.path.join(args.out, n + ".png"))
        print("wrote", n)
    print("TACOGEN_DONE")


main()
