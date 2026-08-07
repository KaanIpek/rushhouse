"""
Fresh 3/4-view dining furniture (PIL) — a NEW table + a separate chair, drawn
from scratch (not cut out of the old painterly table). Rich warm wood + leather
cushion, cylindrical/edge shading so they read at the same ~3/4 angle as the game.

  py furniture_gen.py --out <Art/Objects>
"""
import os, argparse
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args()
R = args.res

WOOD_T = (150, 96, 46)      # lit wood
WOOD_M = (110, 68, 32)      # mid
WOOD_D = (66, 40, 18)       # dark
WOOD_E = (44, 26, 12)       # outline / deep shadow
LEATHER = (128, 66, 38)
LEATHER_L = (176, 100, 60)
LEATHER_D = (78, 38, 20)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shadow(im, box, blur=12, a=110):
    lay = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(lay).ellipse(box, fill=(0, 0, 0, a))
    im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(blur)))


def vgrad(box, top, bot, radius, outline=None, ow=3):
    x0, y0, x1, y1 = [int(v) for v in box]
    lay = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0)
        d.line([(x0, y), (x1, y)], fill=(*lerp(top, bot, t), 255))
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).rounded_rectangle((x0, y0, x1, y1), radius=radius, fill=255)
    lay.putalpha(m)
    if outline:
        ImageDraw.Draw(lay).rounded_rectangle((x0, y0, x1, y1), radius=radius, outline=(*outline, 255), width=ow)
    return lay


def edge_shade(im, box, radius, dark=WOOD_E):
    """darken left/right edges for round volume"""
    x0, y0, x1, y1 = [int(v) for v in box]
    w = x1 - x0
    m = Image.new("L", (R, R), 0)
    ImageDraw.Draw(m).rounded_rectangle((x0, y0, x1, y1), radius=radius, fill=255)
    g = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    gd = ImageDraw.Draw(g)
    cxx = (x0 + x1) / 2
    for x in range(x0, x1):
        t = abs((x - cxx) / (w / 2))
        gd.line([(x, y0), (x, y1)], fill=(*dark, int(120 * t ** 2.4)))
    g.putalpha(ImageChops.multiply(g.split()[3], m))
    im.alpha_composite(g)


def grain(d, box, tone, n=7):
    x0, y0, x1, y1 = [int(v) for v in box]
    for i in range(n):
        y = y0 + int((y1 - y0) * (i + 0.5) / n)
        d.line([(x0 + 6, y), (x1 - 6, y)], fill=(*tone, 46), width=1)


def leg(d, cx, y0, y1, w, top=WOOD_M, bot=WOOD_D):
    for y in range(int(y0), int(y1)):
        t = (y - y0) / max(1, y1 - y0)
        ww = int(w * (1 - 0.18 * t))
        d.line([(cx - ww, y), (cx + ww, y)], fill=(*lerp(top, bot, t), 255))
    d.line([(cx - w + 1, y0), (cx - w + 1, y1)], fill=(*WOOD_T, 120), width=2)   # lit edge
    d.line([(cx + w - 1, y0), (cx + w - 1, y1)], fill=(*WOOD_E, 160), width=2)   # dark edge


def make_table():
    # a thick slab: lit top surface + a tall DARK front face (thickness) + visible legs = real volume
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    shadow(im, (cx - 126, 206, cx + 126, 258), 13, 120)
    d = ImageDraw.Draw(im)
    leg(d, cx - 104, 132, 238, 12)
    leg(d, cx + 104, 132, 238, 12)                        # back legs
    leg(d, cx - 104, 176, 256, 14)
    leg(d, cx + 104, 176, 256, 14)                        # front legs
    top = (cx - 138, 82, cx + 138, 150)                   # top surface
    front = (cx - 138, 148, cx + 138, 202)               # front face (slab thickness)
    im.alpha_composite(vgrad(front, (104, 63, 30), (54, 31, 13), 22, WOOD_E, 3))   # dark front
    im.alpha_composite(vgrad(top, (182, 122, 64), (126, 80, 40), 28, WOOD_E, 3))   # lit top
    d = ImageDraw.Draw(im)
    grain(d, top, (72, 42, 20), 5)
    edge_shade(im, top, 28)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((cx - 136, 143, cx + 136, 157), radius=8, fill=(202, 146, 88))   # bright front lip
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((cx - 112, 90, cx + 112, 120), radius=16, fill=(220, 166, 106, 85))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(7)))                     # top sheen
    return im


# seat top matches the TABLE top finish exactly (same warm wood, so materials read identical)
SEAT_T = (176, 118, 62)
SEAT_D = (120, 76, 38)
SEAT_LIP = (200, 142, 88)


def make_side_chair():
    # 3/4 SIDE dining chair facing RIGHT (backrest on the LEFT) -> for a LEFT-of-table seat.
    # All wood, same palette as the table. Mirror it for the right seat.
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    shadow(im, (cx - 86, 202, cx + 80, 252), 12, 116)
    d = ImageDraw.Draw(im)
    leg(d, cx - 40, 150, 244, 9)                        # back leg (far)
    leg(d, cx + 34, 160, 246, 9)                        # front leg (far)
    leg(d, cx - 58, 152, 248, 12)                       # back leg (near)
    leg(d, cx + 54, 162, 250, 13)                       # front leg (near)
    # tall backrest panel on the LEFT (the sitter faces right, into the table)
    im.alpha_composite(vgrad((cx - 76, 44, cx - 30, 170), WOOD_M, WOOD_D, 15, WOOD_E, 3))
    edge_shade(im, (cx - 76, 44, cx - 30, 170), 15)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((cx - 74, 48, cx - 32, 74), radius=12, fill=(182, 122, 64))   # top-rail highlight
    grain(d, (cx - 74, 60, cx - 32, 162), (70, 42, 20), 7)
    # seat slab reaching RIGHT from the backrest base: front face (thickness) + wood top
    im.alpha_composite(vgrad((cx - 68, 158, cx + 74, 198), (96, 58, 28), (50, 30, 13), 12, WOOD_E, 3))
    im.alpha_composite(vgrad((cx - 68, 130, cx + 74, 164), SEAT_T, SEAT_D, 13, WOOD_E, 3))
    grain(ImageDraw.Draw(im), (cx - 60, 136, cx + 68, 158), (72, 42, 20), 4)
    edge_shade(im, (cx - 68, 130, cx + 74, 164), 13)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((cx - 66, 160, cx + 72, 172), radius=6, fill=SEAT_LIP)         # bright seat-front lip
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - 52, 134, cx + 58, 152), fill=(206, 140, 90, 78))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    return im


def make_front_chair():
    # chunky FRONT-3/4 dining chair (backrest away at top, seat toward viewer) -> for a FAR seat.
    # All wood, same palette as the table.
    im = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    cx = R // 2
    shadow(im, (cx - 78, 204, cx + 78, 252), 12, 118)
    d = ImageDraw.Draw(im)
    leg(d, cx - 58, 118, 214, 10)
    leg(d, cx + 58, 118, 214, 10)                       # back legs
    leg(d, cx - 60, 60, 150, 9)
    leg(d, cx + 60, 60, 150, 9)                         # backrest posts
    im.alpha_composite(vgrad((cx - 64, 54, cx + 64, 142), WOOD_M, WOOD_D, 22, WOOD_E, 4))   # wood backrest
    edge_shade(im, (cx - 64, 54, cx + 64, 142), 22)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((cx - 60, 58, cx + 60, 84), radius=16, fill=(182, 122, 64))   # top-rail highlight
    grain(d, (cx - 58, 70, cx + 58, 138), (70, 42, 20), 5)
    leg(d, cx - 62, 176, 246, 12)
    leg(d, cx + 62, 176, 246, 12)                       # front legs
    im.alpha_composite(vgrad((cx - 82, 166, cx + 82, 202), (96, 58, 28), (50, 30, 13), 14, WOOD_E, 3))  # seat front face
    im.alpha_composite(vgrad((cx - 82, 132, cx + 82, 170), WOOD_M, WOOD_D, 16, WOOD_E, 3))               # seat frame
    im.alpha_composite(vgrad((cx - 72, 132, cx + 72, 164), SEAT_T, SEAT_D, 12, WOOD_E, 2))               # wood seat top
    grain(ImageDraw.Draw(im), (cx - 66, 138, cx + 66, 160), (72, 42, 20), 4)
    edge_shade(im, (cx - 72, 132, cx + 72, 164), 12)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((cx - 80, 163, cx + 80, 175), radius=7, fill=SEAT_LIP)         # bright seat-front lip
    sh = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - 54, 136, cx + 54, 156), fill=(206, 140, 90, 80))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    return im


def main():
    os.makedirs(args.out, exist_ok=True)
    make_table().save(os.path.join(args.out, "dtable.png"))
    side = make_side_chair()                                                        # LEFT seat: faces right
    side.save(os.path.join(args.out, "dchair.png"))
    side.transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(args.out, "dchairR.png"))   # RIGHT seat: faces left
    front = make_front_chair()                                                      # FAR seat: faces viewer
    front.save(os.path.join(args.out, "dchairF.png"))
    front.transpose(Image.FLIP_TOP_BOTTOM).save(os.path.join(args.out, "dchairB.png"))  # NEAR seat: seen from behind
    print("FURNGEN_DONE")


main()
