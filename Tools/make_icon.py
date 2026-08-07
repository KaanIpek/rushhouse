"""Build the iOS/Android app icon from the game's own art.

Apple rejects an upload outright with "Missing Info.plist value CFBundleIconName" when the icon
slots are empty, so this is a release blocker, not decoration. Unity builds the asset catalog from
Player Settings, and Player Settings needs a square source with NO transparency and NO rounded
corners -- Apple applies the mask itself, and an alpha channel in the 1024 marketing icon is a
validation failure.

The art is the game's own deluxe burger render on the menu's warm gradient, so the icon and the
first screenshot look like the same product.

  python Tools/make_icon.py
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
DISH = ROOT / "Assets" / "Resources" / "Art" / "FinalDishes" / "deluxe.png"
OUT_DIR = ROOT / "Assets" / "Resources" / "Icon"
SIZE = 1024


def gradient(size, top, bottom):
    img = Image.new("RGB", (1, size))
    px = img.load()
    for y in range(size):
        t = y / (size - 1)
        px[0, y] = tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
    return img.resize((size, size), Image.BICUBIC)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    # Warm kitchen gradient, matching the menu hero rather than a random brand colour.
    icon = gradient(SIZE, (250, 176, 62), (208, 74, 42))

    # A soft light from the top-left so the flat gradient reads as a lit surface.
    glow = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(glow).ellipse([-SIZE * .30, -SIZE * .38, SIZE * .78, SIZE * .70], fill=110)
    icon = Image.composite(Image.new("RGB", (SIZE, SIZE), (255, 232, 180)), icon,
                           glow.filter(ImageFilter.GaussianBlur(SIZE * .10)))

    dish = Image.open(DISH).convert("RGBA")
    # Trim the transparent border so the burger fills the icon instead of floating in it.
    bbox = dish.getbbox()
    if bbox:
        dish = dish.crop(bbox)
    target = int(SIZE * 0.74)
    scale = target / max(dish.size)
    dish = dish.resize((max(1, round(dish.width * scale)), max(1, round(dish.height * scale))),
                       Image.LANCZOS)

    # Drop shadow: without one the burger looks pasted onto the gradient.
    shadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    sx = (SIZE - dish.width) // 2
    sy = (SIZE - dish.height) // 2 + int(SIZE * .03)
    shadow.paste((0, 0, 0, 130), (sx, sy + int(SIZE * .035)), dish)
    shadow = shadow.filter(ImageFilter.GaussianBlur(SIZE * .028))
    icon = Image.alpha_composite(icon.convert("RGBA"), shadow)
    icon.alpha_composite(dish, (sx, sy))

    # FLATTEN. An alpha channel in the App Store icon fails validation.
    flat = Image.new("RGB", (SIZE, SIZE), (255, 255, 255))
    flat.paste(icon, mask=icon.split()[3])

    out = OUT_DIR / "app_icon.png"
    flat.save(out)
    print(f"ICON_DONE {out} {flat.size} mode={flat.mode}")


if __name__ == "__main__":
    main()
