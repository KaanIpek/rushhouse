"""
Chunky 3D-looking room walls (PIL). Each wall reads as a raised block: a lit TOP
surface, a sharp highlight edge, then a tall FRONT FACE that shades from light to dark
(the wall's visible height), ending in a crisp contact shadow at the floor. This gives
real dimensionality without an isometric skew (the floor stays axis-aligned top-down).

  py wall_gen.py --out <Art/Objects>
"""
import os, argparse
from PIL import Image, ImageDraw

ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
args = ap.parse_args()

OUT    = (108, 82, 50)     # outer trim (meets grass)
TOPLIT = (224, 192, 142)   # lit top surface of the wall
LIPHI  = (250, 233, 197)   # sharp highlight where the top meets the front face
FACEHI = (178, 140, 98)    # front face, upper (nearer the light)
FACELO = (86, 64, 42)      # front face, lower (in shadow) — this darkening = perceived height
DROP   = (30, 22, 14)      # crisp contact shadow where the wall meets the floor


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def profile(n, outW=0.09, topW=0.24, lipW=0.03, dropW=0.08):
    """color per step 0..n-1 going OUTER/grass(0) -> INNER/floor(1). A tall shaded
    front face between the lit top and the floor sells the 3D height."""
    faceStart = outW + topW + lipW
    faceEnd = 1.0 - dropW
    out = []
    for i in range(n):
        t = i / (n - 1)
        if t < outW:
            c = lerp(OUT, TOPLIT, (t / outW) ** 0.7)                       # trim rising to the lit top
        elif t < outW + topW:
            c = lerp(TOPLIT, lerp(TOPLIT, LIPHI, .5), (t - outW) / topW)   # lit top, brightening toward the edge
        elif t < faceStart:
            c = LIPHI                                                       # sharp highlight edge
        elif t < faceEnd:
            u = (t - faceStart) / max(1e-3, faceEnd - faceStart)
            c = lerp(FACEHI, FACELO, u ** 0.85)                            # TALL front face: light -> dark
        else:
            c = DROP                                                        # contact shadow at the floor
        out.append(c)
    return out


def side_wall(W=340, H=900):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cols = profile(W)
    for x in range(W):
        d.line([(x, 0), (x, H)], fill=(*cols[x], 255))
    return im


def back_wall(W=1500, H=360):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cols = profile(H)
    for y in range(H):
        d.line([(0, y), (W, y)], fill=(*cols[y], 255))
    return im


def corner(S=300):
    # square that caps the room corner: lit top wrapping to a shaded face + highlight edge
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    px = im.load()
    for x in range(S):
        for y in range(S):
            tx = x / (S - 1)                 # 0 outer-left .. 1 inner-right (floor)
            ty = y / (S - 1)                 # 0 top(grass) .. 1 bottom(floor)
            di = min(1.0 - tx, 1.0 - ty)     # distance from the inner (floor) corner edges
            if di < 0.08:
                c = DROP
            elif di < 0.13:
                c = LIPHI
            elif di < 0.5:
                c = lerp(FACEHI, FACELO, (0.5 - di) / 0.37)   # shaded face near the inner edges
            else:
                c = lerp(TOPLIT, LIPHI, (1 - (tx + ty) * .5) * .35)   # lit top
            px[x, y] = (*c, 255)
    return im


def main():
    os.makedirs(args.out, exist_ok=True)
    cn = corner()
    cn.save(os.path.join(args.out, "wallCorner.png"))
    cn.transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(args.out, "wallCornerR.png"))
    sw = side_wall()
    sw.save(os.path.join(args.out, "wallSideL.png"))
    sw.transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(args.out, "wallSideR.png"))
    back_wall().save(os.path.join(args.out, "wallBackH.png"))
    print("WALLGEN_DONE")


main()
