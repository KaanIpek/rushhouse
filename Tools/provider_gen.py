"""
Per-ingredient provider (supply station) generator — PIL.
Each ingredient gets its OWN complete image: an appropriate container FILLED
with that ingredient, instead of one shared shelf image with an icon on top.

  - cold goods (meat/sausage/cheese/milk/sauce/coffee) -> steel freezer pan
  - produce & bread (bun/dough/lettuce/tomato/onion)   -> warm wooden crate

  py provider_gen.py --foods <Art/Foods> --out <Art/Objects>
"""
import os, argparse, random, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ap = argparse.ArgumentParser()
ap.add_argument("--foods", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args()
R = args.res

# ingredient -> source food sprite
SRC = {
    "bun": "bun", "patty": "pattyRaw", "lettuce": "lettuce", "tomato": "tomato",
    "cheese": "cheese", "sauce": "sauce", "dough": "dough", "coffee": "coffee",
    "milk": "milk", "sausage": "sausageRaw", "onion": "onion", "rice": "rice",
}
COLD = {"patty", "sausage", "cheese", "milk", "sauce", "coffee"}   # steel freezer
WOOD = {"bun", "dough", "lettuce", "tomato", "onion"}             # wooden crate


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def vgrad_rrect(size, box, top, bot, radius, outline=None, ow=3):
    """A vertical-gradient rounded rectangle onto its own RGBA layer."""
    x0, y0, x1, y1 = box
    lay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    h = max(1, y1 - y0)
    for y in range(y0, y1):
        t = (y - y0) / h
        d.line([(x0, y), (x1, y)], fill=(*lerp(top, bot, t), 255))
    # mask to rounded rect
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
    lay.putalpha(mask)
    if outline:
        od = ImageDraw.Draw(lay)
        od.rounded_rectangle(box, radius=radius, outline=(*outline, 255), width=ow)
    return lay


def brighten(im, f):
    if f == 1.0:
        return im
    r, g, b, a = im.split()
    lut = [min(255, int(i * f)) for i in range(256)]
    return Image.merge("RGBA", (r.point(lut), g.point(lut), b.point(lut), a))


def soft_shadow(size, box, blur=9, alpha=120):
    lay = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).ellipse(box, fill=(0, 0, 0, alpha))
    return lay.filter(ImageFilter.GaussianBlur(blur))


def _cyl_shade(im, box, light, dark, radius):
    """Overlay cylindrical volume shading (dark curved edges + a top specular band)."""
    x0, y0, x1, y1 = [int(v) for v in box]
    w = x1 - x0
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).rounded_rectangle((x0, y0, x1, y1), radius=radius, fill=255)
    grad = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    dd = ImageDraw.Draw(grad)
    cxx = (x0 + x1) / 2
    for x in range(x0, x1):
        t = abs((x - cxx) / (w / 2))
        dd.line([(x, y0), (x, y1)], fill=(*dark, int(155 * t ** 2.3)))
    grad.putalpha(ImageChops.multiply(grad.split()[3], m))
    im.alpha_composite(grad)
    sp = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sp).ellipse((int(x0 + w * 0.16), y0 + 6, int(x1 - w * 0.16), int(y0 + (y1 - y0) * 0.30)), fill=(*light, 90))
    sp.putalpha(ImageChops.multiply(sp.split()[3], m))
    im.alpha_composite(sp.filter(ImageFilter.GaussianBlur(8)))


def _rim(d, cx, rimY, rx, ry, outer, mid, inner, hi):
    d.ellipse((cx - rx, rimY - ry, cx + rx, rimY + ry), fill=outer)
    d.ellipse((cx - rx + 4, rimY - ry + 3, cx + rx - 4, rimY + ry - 1), fill=mid)
    ii, iy = int(rx * 0.85), int(ry * 0.78)
    d.ellipse((cx - ii, rimY - iy + 5, cx + ii, rimY + iy + 6), fill=inner)  # dark interior
    d.arc((cx - rx + 2, rimY - ry + 1, cx + rx - 2, rimY + ry - 1), 200, 340, fill=hi, width=3)  # top-lit edge
    return ii, iy


def steel_pan(cold=True):
    """Steel gastronorm pan seen high-angle (3-D volume); returns (layer, opening_box)."""
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    rimY, botY = int(R * 0.40), int(R * 0.85)
    rx, ry = int(R * 0.39), int(R * 0.15)
    im.alpha_composite(soft_shadow((R, R), (cx - rx - 6, int(R * 0.80), cx + rx + 6, int(R * 0.95)), 13, 110))
    # cold = a white chest FREEZER; warm = brushed-steel bin
    top = (232, 240, 248) if cold else (150, 156, 166)
    bot = (150, 172, 196) if cold else (52, 56, 64)
    im.alpha_composite(vgrad_rrect((R, R), (cx - rx, rimY, cx + rx, botY), top, bot, radius=22, outline=(80, 96, 116) if cold else (28, 30, 36), ow=3))
    d = ImageDraw.Draw(im)
    if cold:
        d.rounded_rectangle((cx - rx + 14, botY - 34, cx - rx + 78, botY - 18), radius=5, fill=(120, 140, 164))   # freezer handle/panel
        d.rectangle((cx - rx + 20, botY - 30, cx - rx + 40, botY - 22), fill=(70, 150, 90))                        # green LED
    else:
        for k in range(-4, 5):
            x = cx + int(rx * k * 0.2)
            d.line([(x, rimY + 12), (x, botY - 4)], fill=(224, 232, 244, 26), width=2)
    _cyl_shade(im, (cx - rx, rimY, cx + rx, botY), (255, 255, 255), (30, 48, 72) if cold else (18, 22, 30), 22)
    ii, iy = _rim(d, cx, rimY, rx, ry,
                  (224, 236, 248) if cold else (196, 202, 214),
                  (176, 200, 224) if cold else (140, 148, 162),
                  (150, 186, 214) if cold else (34, 40, 50), (250, 253, 255, 220))
    if cold:
        haze = Image.new("RGBA", (R, R), (0, 0, 0, 0))
        ImageDraw.Draw(haze).ellipse((cx - ii, rimY - iy, cx + ii, rimY + iy), fill=(214, 238, 255, 120))
        im.alpha_composite(haze.filter(ImageFilter.GaussianBlur(6)))
    opening = (cx - ii, rimY - int(ry * 1.4), cx + ii, rimY + int(ry * 0.55))
    return im, opening


def wood_crate():
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    rimY, botY = int(R * 0.40), int(R * 0.85)
    rx, ry = int(R * 0.4), int(R * 0.15)
    im.alpha_composite(soft_shadow((R, R), (cx - rx - 6, int(R * 0.80), cx + rx + 6, int(R * 0.95)), 13, 110))
    im.alpha_composite(vgrad_rrect((R, R), (cx - rx, rimY, cx + rx, botY), (170, 114, 60), (80, 48, 22), radius=12, outline=(44, 26, 12), ow=3))
    d = ImageDraw.Draw(im)
    # vertical planks
    planks = 4
    pw = (2 * rx) / planks
    for k in range(1, planks):
        px = int(cx - rx + k * pw)
        d.line([(px, rimY + 4), (px, botY - 3)], fill=(46, 26, 12, 175), width=3)
        d.line([(px + 2, rimY + 4), (px + 2, botY - 3)], fill=(198, 140, 84, 70), width=1)
    # horizontal straps
    for t in (0.36, 0.74):
        hy = int(rimY + (botY - rimY) * t)
        d.rectangle((cx - rx + 2, hy, cx + rx - 2, hy + 5), fill=(58, 34, 16, 185))
        d.line([(cx - rx + 2, hy), (cx + rx - 2, hy)], fill=(196, 138, 82, 80), width=1)
    _cyl_shade(im, (cx - rx, rimY, cx + rx, botY), (255, 226, 172), (28, 15, 5), 12)
    # 3-D corner posts
    for sx in (-1, 1):
        x = cx + sx * (rx - 7)
        d.line([(x, rimY + 3), (x, botY - 2)], fill=(64, 38, 18, 225), width=13)
        d.line([(x - sx * 5, rimY + 3), (x - sx * 5, botY - 2)], fill=(152, 102, 58, 120), width=3)
    ii, iy = _rim(d, cx, rimY, rx, ry, (158, 104, 54), (120, 76, 38), (38, 22, 10), (214, 160, 102, 190))
    opening = (cx - ii, rimY - int(ry * 1.4), cx + ii, rimY + int(ry * 0.55))
    return im, opening


def fill_pile(im, food, opening, count, base_w):
    ox0, oy0, ox1, oy1 = opening
    ocx = (ox0 + ox1) // 2
    span = (ox1 - ox0)
    # inner pile shadow
    sh = soft_shadow((R, R), (ox0 + 10, oy1 - 22, ox1 - 10, oy1 + 8), 7, 90)
    im.alpha_composite(sh)
    # layout: back row (higher, smaller, dimmer) then front row (lower, bigger)
    rows = [
        dict(y=oy0 + int((oy1 - oy0) * 0.30), n=max(2, count // 2), s=0.82, b=0.80),
        dict(y=oy0 + int((oy1 - oy0) * 0.72), n=count - max(2, count // 2), s=1.0, b=1.0),
    ]
    for ri, row in enumerate(rows):
        n = row["n"]
        for i in range(n):
            fx = ocx + int((span * 0.32) * (((i + 0.5) / n) * 2 - 1))
            fx += random.randint(-10, 10)
            w = int(base_w * row["s"] * random.uniform(0.94, 1.06))
            f = food.copy()
            f.thumbnail((w, w), Image.LANCZOS)
            f = brighten(f, row["b"])
            im.alpha_composite(f, (fx - f.width // 2, row["y"] - f.height // 2))


def draw_bottle(im, cx, base_y, w, h, body, cap):
    d = ImageDraw.Draw(im)
    im.alpha_composite(vgrad_rrect((R, R), (cx - w // 2, base_y - h, cx + w // 2, base_y),
                       lerp(body, (255, 255, 255), 0.30), lerp(body, (0, 0, 0), 0.30),
                       radius=min(w // 2 - 2, 20), outline=lerp(body, (0, 0, 0), 0.5), ow=3))
    d.line([(cx - w // 2 + 7, base_y - h + 12), (cx - w // 2 + 7, base_y - 14)], fill=(255, 255, 255, 110), width=4)
    nw = int(w * 0.42)
    topy = base_y - h
    d.rounded_rectangle((cx - nw // 2, topy - int(h * 0.14), cx + nw // 2, topy + 6), radius=5, fill=lerp(body, (255, 255, 255), 0.1))
    d.rounded_rectangle((cx - nw // 2 - 1, topy - int(h * 0.30), cx + nw // 2 + 1, topy - int(h * 0.12)), radius=5, fill=cap, outline=lerp(cap, (0, 0, 0), 0.4), width=2)
    d.polygon([(cx - 5, topy - int(h * 0.30)), (cx + 5, topy - int(h * 0.30)), (cx, topy - int(h * 0.42))], fill=lerp(cap, (0, 0, 0), 0.3))


def sauce_provider():
    im, opening = steel_pan(cold=False)
    ox0, oy0, ox1, oy1 = opening
    ocx = (ox0 + ox1) // 2
    base = oy1 + 8
    trio = [((202, 44, 34), (58, 58, 62)), ((228, 182, 44), (58, 58, 62)), ((236, 232, 222), (196, 62, 50))]
    xs = [ocx - int((ox1 - ox0) * 0.27), ocx, ocx + int((ox1 - ox0) * 0.27)]
    for x, (body, cap) in zip(xs, trio):
        draw_bottle(im, x, base, int(R * 0.16), int(R * 0.34), body, cap)
    return im


def milk_provider():
    im, opening = steel_pan(cold=True)
    ox0, oy0, ox1, oy1 = opening
    ocx = (ox0 + ox1) // 2
    base = oy1 + 8
    xs = [ocx - int((ox1 - ox0) * 0.26), ocx + int((ox1 - ox0) * 0.02), ocx + int((ox1 - ox0) * 0.3)]
    for i, x in enumerate(xs):
        draw_bottle(im, x, base - (i % 2) * 8, int(R * 0.15), int(R * 0.37), (238, 240, 246), (72, 122, 202))
    return im


def coffee_provider():
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    top_y, bot_y = int(R * 0.34), int(R * 0.86)
    bw, nw = int(R * 0.37), int(R * 0.21)
    burlap, burlap_d, burlap_l = (156, 130, 84), (110, 90, 56), (200, 172, 120)
    im.alpha_composite(soft_shadow((R, R), (cx - bw - 6, int(R * 0.80), cx + bw + 6, int(R * 0.95)), 13, 110))
    d = ImageDraw.Draw(im)
    neck_y = top_y + int((bot_y - top_y) * 0.16)
    poly = [(cx - nw, top_y + 6), (cx + nw, top_y + 6), (cx + bw, neck_y + 10),
            (cx + int(bw * 0.97), bot_y - 14), (cx + int(bw * 0.66), bot_y),
            (cx - int(bw * 0.66), bot_y), (cx - int(bw * 0.97), bot_y - 14), (cx - bw, neck_y + 10)]
    d.polygon(poly, fill=burlap)
    # cylindrical volume shading masked to the sack silhouette
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).polygon(poly, fill=255)
    grad = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for x in range(cx - bw, cx + bw):
        t = abs((x - cx) / bw)
        gd.line([(x, top_y), (x, bot_y)], fill=(*burlap_d, int(150 * t ** 2.2)))
    grad.putalpha(ImageChops.multiply(grad.split()[3], m))
    im.alpha_composite(grad)
    sp = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sp).ellipse((cx - nw, neck_y, cx + nw, neck_y + int((bot_y - neck_y) * 0.42)), fill=(*burlap_l, 65))
    sp.putalpha(ImageChops.multiply(sp.split()[3], m))
    im.alpha_composite(sp.filter(ImageFilter.GaussianBlur(9)))
    for gy in range(neck_y, bot_y, 10):
        d.line([(cx - bw, gy), (cx + bw, gy)], fill=(108, 88, 54, 30), width=1)
    for fx in (-0.55, -0.18, 0.18, 0.55):
        d.line([(cx + int(nw * fx), neck_y), (cx + int(bw * fx * 0.82), bot_y - 18)], fill=(110, 90, 56, 70), width=2)
    # rolled, lit top rim
    d.ellipse((cx - nw - 10, top_y - 12, cx + nw + 10, top_y + 18), fill=(150, 124, 80), outline=burlap_d, width=3)
    d.arc((cx - nw - 10, top_y - 12, cx + nw + 10, top_y + 18), 200, 340, fill=(208, 180, 128, 200), width=3)
    d.ellipse((cx - nw + 4, top_y - 4, cx + nw - 4, top_y + 14), fill=(48, 34, 22))
    for _ in range(95):
        bx = cx + random.randint(-nw - 3, nw + 3)
        by = top_y + random.randint(-11, 9)
        r = random.randint(4, 7)
        col = random.choice([(82, 50, 30), (100, 62, 36), (64, 40, 24)])
        d.ellipse((bx - r, by - r, bx + r, by + r), fill=col)
        d.line([(bx, by - r + 1), (bx, by + r - 1)], fill=(44, 28, 16), width=1)
    return im


def rice_provider():
    # burlap sack of white rice grains (coffee-sack silhouette, lighter cloth + rice)
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    top_y, bot_y = int(R * 0.34), int(R * 0.86)
    bw, nw = int(R * 0.37), int(R * 0.21)
    burlap, burlap_d, burlap_l = (196, 178, 132), (150, 132, 92), (232, 218, 180)
    im.alpha_composite(soft_shadow((R, R), (cx - bw - 6, int(R * 0.80), cx + bw + 6, int(R * 0.95)), 13, 110))
    d = ImageDraw.Draw(im)
    neck_y = top_y + int((bot_y - top_y) * 0.16)
    poly = [(cx - nw, top_y + 6), (cx + nw, top_y + 6), (cx + bw, neck_y + 10),
            (cx + int(bw * 0.97), bot_y - 14), (cx + int(bw * 0.66), bot_y),
            (cx - int(bw * 0.66), bot_y), (cx - int(bw * 0.97), bot_y - 14), (cx - bw, neck_y + 10)]
    d.polygon(poly, fill=burlap)
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).polygon(poly, fill=255)
    grad = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for x in range(cx - bw, cx + bw):
        t = abs((x - cx) / bw)
        gd.line([(x, top_y), (x, bot_y)], fill=(*burlap_d, int(140 * t ** 2.2)))
    grad.putalpha(ImageChops.multiply(grad.split()[3], m))
    im.alpha_composite(grad)
    sp = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sp).ellipse((cx - nw, neck_y, cx + nw, neck_y + int((bot_y - neck_y) * 0.42)), fill=(*burlap_l, 70))
    sp.putalpha(ImageChops.multiply(sp.split()[3], m))
    im.alpha_composite(sp.filter(ImageFilter.GaussianBlur(9)))
    for gy in range(neck_y, bot_y, 10):
        d.line([(cx - bw, gy), (cx + bw, gy)], fill=(150, 132, 92, 28), width=1)
    d.ellipse((cx - nw - 10, top_y - 12, cx + nw + 10, top_y + 18), fill=(188, 170, 124), outline=burlap_d, width=3)
    d.arc((cx - nw - 10, top_y - 12, cx + nw + 10, top_y + 18), 200, 340, fill=(238, 226, 190, 200), width=3)
    d.ellipse((cx - nw + 4, top_y - 4, cx + nw - 4, top_y + 14), fill=(120, 106, 78))
    for _ in range(120):
        bx = cx + random.randint(-nw - 2, nw + 2)
        by = top_y + random.randint(-11, 9)
        col = random.choice([(244, 238, 222), (226, 218, 196), (252, 248, 238)])
        d.ellipse((bx - 2, by - 3, bx + 2, by + 3), fill=(*col, 255))
    return im


def make(name):
    random.seed(hash(name) & 0xffff)
    if name == "sauce":
        return sauce_provider()
    if name == "milk":
        return milk_provider()
    if name == "coffee":
        return coffee_provider()
    if name == "rice":
        return rice_provider()
    food = Image.open(os.path.join(args.foods, SRC[name] + ".png")).convert("RGBA")
    cold = name in COLD
    if name in WOOD:
        im, opening = wood_crate()
    else:
        im, opening = steel_pan(cold=cold)
    # counts / sizes tuned a bit per ingredient
    count = 6
    base_w = int(R * 0.34)
    if name in ("bun", "patty", "dough"):
        base_w = int(R * 0.40)
        count = 5
    if name in ("milk", "coffee", "sauce"):
        base_w = int(R * 0.32)
        count = 5
    fill_pile(im, food, opening, count, base_w)
    return im


def main():
    os.makedirs(args.out, exist_ok=True)
    for name in SRC:
        im = make(name)
        outname = "provider" + name[0].upper() + name[1:]
        im.save(os.path.join(args.out, outname + ".png"))
        print("wrote", outname)
    print("PROVIDERGEN_DONE")


main()
