"""Generate clean flat UI icons (white glyph on transparent) for the floorplan buttons.
White so the game can tint them. 128x128 PNGs into Assets/Resources/Art/UI/."""
import os, math
from PIL import Image, ImageDraw

OUT = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\outputs\rushhouse-unity\Assets\Resources\Art\UI"
os.makedirs(OUT, exist_ok=True)
S = 128
W = (255, 255, 255, 255)


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))
    print("icon", name)


def rotate():
    img = canvas(); d = ImageDraw.Draw(img)
    cx, cy, r, wd = 64, 66, 34, 13
    # a ~300deg arc (open at top-right) + arrowhead
    d.arc([cx - r, cy - r, cx + r, cy + r], start=-40, end=210, fill=W, width=wd)
    # arrowhead at the arc end (top-right, angle -40deg)
    a = math.radians(-40); ex, ey = cx + r * math.cos(a), cy + r * math.sin(a)
    d.polygon([(ex + 20, ey - 2), (ex - 8, ey - 20), (ex - 2, ey + 16)], fill=W)
    return img


def chair():
    img = canvas(); d = ImageDraw.Draw(img)
    # simple armchair silhouette
    d.rounded_rectangle([34, 30, 94, 74], radius=12, fill=W)        # backrest
    d.rounded_rectangle([28, 60, 100, 88], radius=10, fill=W)       # seat
    d.rectangle([32, 86, 42, 104], fill=W)                          # legs
    d.rectangle([86, 86, 96, 104], fill=W)
    return img


def reset():
    img = canvas(); d = ImageDraw.Draw(img)
    cx, cy, r, wd = 64, 64, 34, 13
    # counter-clockwise refresh arc + arrowhead (open at left)
    d.arc([cx - r, cy - r, cx + r, cy + r], start=110, end=390, fill=W, width=wd)
    a = math.radians(110); ex, ey = cx + r * math.cos(a), cy + r * math.sin(a)
    d.polygon([(ex - 20, ey - 4), (ex + 6, ey - 20), (ex + 4, ey + 16)], fill=W)
    return img


def back():
    img = canvas(); d = ImageDraw.Draw(img)
    d.line([(82, 30), (44, 64), (82, 98)], fill=W, width=16, joint="curve")
    return img


def play():
    img = canvas(); d = ImageDraw.Draw(img)
    d.polygon([(44, 32), (44, 96), (100, 64)], fill=W)             # play triangle
    return img


def pause():
    img = canvas(); d = ImageDraw.Draw(img)
    d.rounded_rectangle([40, 34, 58, 94], radius=5, fill=W)
    d.rounded_rectangle([70, 34, 88, 94], radius=5, fill=W)
    return img


save(rotate(), "ic_rotate")
save(chair(), "ic_seats")
save(reset(), "ic_reset")
save(back(), "ic_back")
save(play(), "ic_open")
save(pause(), "ic_pause")
print("DONE")
