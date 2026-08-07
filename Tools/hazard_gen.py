"""
Hazard sprites (PIL): a layered flame + a red fire-extinguisher canister.
  py hazard_gen.py --out <Art/Objects>
"""
import os, argparse
from PIL import Image, ImageDraw, ImageFilter

ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
args = ap.parse_args()


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def flame(R=320):
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx, base = R // 2, int(R * .80)
    # teardrop outline (tip up, rounded bottom), scaled toward the base for each inner layer
    raw = [(0, -232), (48, -132), (60, -52), (34, 22), (0, 44), (-34, 22), (-60, -52), (-48, -132)]

    def poly(s):
        return [(cx + x * s, base + y * s) for (x, y) in raw]

    layers = [(1.00, (222, 46, 20, 255)), (0.74, (255, 120, 24, 255)),
              (0.48, (255, 206, 60, 255)), (0.24, (255, 248, 210, 255))]
    for s, col in layers:
        lay = Image.new("RGBA", (R, R), (0, 0, 0, 0))
        ImageDraw.Draw(lay).polygon(poly(s), fill=col)
        im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(4)))
    return im


def extinguisher(R=320):
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = R // 2
    RED, RED_L, RED_D = (196, 34, 30), (236, 96, 78), (120, 18, 16)
    blk, steel = (26, 26, 30), (150, 150, 160)
    # ground shadow
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - 70, 262, cx + 70, 298), fill=(0, 0, 0, 120))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(9)))
    # body (cylinder)
    bx0, bx1, by0, by1 = cx - 58, cx + 58, 96, 274
    for x in range(bx0, bx1):
        t = abs((x - cx) / 58)
        col = lerp(RED_L, RED, min(1, t * 1.2))
        if t > .72:
            col = lerp(col, RED_D, (t - .72) / .28)
        d.line([(x, by0), (x, by1)], fill=(*col, 255))
    d.rounded_rectangle((bx0, by0, bx1, by1), radius=40, outline=(*RED_D, 255), width=4)
    # rounded bottom cap sheen
    d.ellipse((bx0 + 8, by1 - 34, bx1 - 8, by1 + 6), fill=(*RED_D, 180))
    # cream label with a flame mark
    d.rounded_rectangle((cx - 44, 150, cx + 44, 220), radius=10, fill=(238, 232, 214))
    d.rounded_rectangle((cx - 44, 150, cx + 44, 220), radius=10, outline=(120, 110, 96), width=2)
    im.alpha_composite(flame().resize((60, 60)), (cx - 30, 156))
    # neck + black valve head + carry handle
    d.rectangle((cx - 20, 74, cx + 20, 98), fill=steel)
    d.rounded_rectangle((cx - 30, 52, cx + 30, 82), radius=8, fill=blk)
    d.arc((cx - 6, 26, cx + 62, 78), start=200, end=20, fill=blk, width=12)     # handle loop
    # hose sweeping to the side ending in a nozzle
    d.line([(cx + 26, 66), (cx + 74, 96), (cx + 60, 150), (cx + 86, 196)], fill=blk, width=11, joint="curve")
    d.ellipse((cx + 74, 190, cx + 100, 214), fill=(40, 40, 46))
    return im


def main():
    os.makedirs(args.out, exist_ok=True)
    flame().save(os.path.join(args.out, "flame.png"))
    extinguisher().save(os.path.join(args.out, "extinguisher.png"))
    print("HAZARD_DONE")


main()
