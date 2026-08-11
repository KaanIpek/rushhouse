"""Build the iOS/Android app icon from the game's own art.

Apple rejects an upload outright with "Missing Info.plist value CFBundleIconName" when the icon
slots are empty, so this is a release blocker, not decoration. Unity builds the asset catalog from
Player Settings, and Player Settings needs a square source with NO transparency and NO rounded
corners -- Apple applies the mask itself, and an alpha channel in the 1024 marketing icon is a
validation failure.

WHY THIS IS NOT THE WARM GRADIENT ANY MORE. The first icon put the burger render on the menu's
orange gradient. Measured at the size it is actually seen, burger and background differed by 17
points of luminance out of 255 -- so at 60px the whole thing collapsed into one warm blob with no
silhouette. It also looked like every other cooking game on the shelf, which are almost uniformly
orange, so it lost twice: unreadable small, invisible in a grid.

The fix is tonal, not decorative: put warm food on a cool ground. Deep teal separates from a
toasted bun in luminance AND hue, which is what makes the shape survive being shrunk, and it reads
as a retro diner rather than as a stock food photo. Everything else here serves the silhouette --
the sunburst and plate aim the eye at the centre, the warm halo lifts the burger off the ground,
and the vignette stops the corners competing.

  python Tools/make_icon.py
"""

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
DISH = ROOT / "Assets" / "Resources" / "Art" / "FinalDishes" / "deluxe.png"
OUT_DIR = ROOT / "Assets" / "Resources" / "Icon"
SIZE = 1024

TEAL_TOP = (18, 106, 114)
TEAL_BOTTOM = (5, 48, 55)
RAY = (32, 143, 150)
GLOW = (255, 178, 77)
PLATE = (250, 242, 226)
PLATE_EDGE = (214, 198, 172)


def vertical_gradient(size, top, bottom):
    strip = Image.new("RGB", (1, size))
    px = strip.load()
    for y in range(size):
        t = y / (size - 1)
        px[0, y] = tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
    return strip.resize((size, size), Image.BICUBIC)


def radial_mask(size, cx, cy, radius, feather):
    """A soft round mask, used for glows and the vignette."""
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=255)
    return m.filter(ImageFilter.GaussianBlur(feather))


def sunburst(size, cx, cy, wedges=18):
    """Retro diner rays. Deliberately near-invisible: at 8% they add depth, at 20% they add noise
    and start to strobe when the icon is scaled down."""
    ss = size * 2  # supersampled so the wedge edges do not alias into stair-steps
    m = Image.new("L", (ss, ss), 0)
    d = ImageDraw.Draw(m)
    step = 360 / wedges
    for i in range(wedges):
        a0 = i * step
        d.pieslice([cx * 2 - ss, cy * 2 - ss, cx * 2 + ss, cy * 2 + ss], a0, a0 + step / 2, fill=255)
    return m.resize((size, size), Image.LANCZOS)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    cx, cy = SIZE // 2, int(SIZE * 0.46)

    icon = vertical_gradient(SIZE, TEAL_TOP, TEAL_BOTTOM).convert("RGBA")

    # --- retro rays -------------------------------------------------------------------------
    # Faded out towards the edges. Rays that run all the way into the corners fight the vignette
    # and, once the squircle mask clips them, leave four visibly chopped wedges.
    rays = Image.new("RGBA", (SIZE, SIZE), RAY + (255,))
    falloff = radial_mask(SIZE, cx, cy, int(SIZE * 0.40), SIZE * 0.22)
    rays.putalpha(ImageChops.multiply(sunburst(SIZE, cx, cy), falloff).point(lambda v: int(v * 0.16)))
    icon = Image.alpha_composite(icon, rays)

    # --- warm pool of light the burger will sit in ------------------------------------------
    glow = Image.new("RGBA", (SIZE, SIZE), GLOW + (255,))
    glow.putalpha(radial_mask(SIZE, cx, cy, int(SIZE * 0.34), SIZE * 0.13)
                  .point(lambda v: int(v * 0.42)))
    icon = Image.alpha_composite(icon, glow)

    # --- plate ------------------------------------------------------------------------------
    # A cream disc gives the burger something to stand on and adds a second bright shape, so the
    # icon still has structure once the burger detail is gone at small sizes.
    plate_y = int(SIZE * 0.655)
    pw, ph = int(SIZE * 0.70), int(SIZE * 0.205)
    plate = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pd = ImageDraw.Draw(plate)
    pd.ellipse([cx - pw // 2, plate_y - ph // 2, cx + pw // 2, plate_y + ph // 2],
               fill=PLATE_EDGE + (255,))
    inset = int(SIZE * 0.022)
    pd.ellipse([cx - pw // 2 + inset, plate_y - ph // 2 + inset,
                cx + pw // 2 - inset, plate_y + ph // 2 - inset], fill=PLATE + (255,))
    # Drop the plate away from the background so it does not look like a sticker.
    pshadow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pshadow.paste((0, 20, 24, 150), (0, int(SIZE * 0.018)), plate.split()[3])
    icon = Image.alpha_composite(icon, pshadow.filter(ImageFilter.GaussianBlur(SIZE * 0.02)))
    icon = Image.alpha_composite(icon, plate)

    # --- the burger -------------------------------------------------------------------------
    dish = Image.open(DISH).convert("RGBA")
    bbox = dish.getbbox()
    if bbox:
        dish = dish.crop(bbox)
    target = int(SIZE * 0.66)
    scale = target / max(dish.size)
    dish = dish.resize((max(1, round(dish.width * scale)), max(1, round(dish.height * scale))),
                       Image.LANCZOS)
    # The render is lit for a game camera, which is flatter than an icon wants.
    dish = ImageEnhance.Color(dish).enhance(1.18)
    dish = ImageEnhance.Contrast(dish).enhance(1.12)

    dx = (SIZE - dish.width) // 2
    dy = plate_y - dish.height + int(SIZE * 0.055)
    alpha = dish.split()[3]

    # Warm halo: the single biggest win for small-size legibility. A dilated, blurred copy of the
    # silhouette in a light warm tone means the burger never touches the teal directly, so its
    # outline survives even when every internal detail is gone.
    halo_src = Image.new("L", (SIZE, SIZE), 0)
    halo_src.paste(alpha, (dx, dy))
    halo = Image.new("RGBA", (SIZE, SIZE), (255, 214, 150, 255))
    halo.putalpha(ImageChops.lighter(
        halo_src.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(SIZE * 0.018)),
        halo_src).point(lambda v: int(v * 0.55)))
    icon = Image.alpha_composite(icon, halo)

    # Contact shadow so the burger sits ON the plate rather than hovering over it.
    contact = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(contact).ellipse(
        [cx - int(SIZE * 0.22), plate_y - int(SIZE * 0.032),
         cx + int(SIZE * 0.22), plate_y + int(SIZE * 0.042)], fill=(60, 30, 10, 120))
    icon = Image.alpha_composite(icon, contact.filter(ImageFilter.GaussianBlur(SIZE * 0.022)))

    icon.alpha_composite(dish, (dx, dy))

    # --- vignette ---------------------------------------------------------------------------
    # Corners are where the squircle mask bites, so darkening them costs nothing and stops the
    # background competing with the centre.
    vig = Image.new("RGBA", (SIZE, SIZE), (0, 18, 22, 255))
    vig.putalpha(ImageChops.invert(radial_mask(SIZE, cx, int(SIZE * 0.5), int(SIZE * 0.62),
                                               SIZE * 0.16)).point(lambda v: int(v * 0.55)))
    icon = Image.alpha_composite(icon, vig)

    # FLATTEN. An alpha channel in the App Store icon fails validation.
    flat = Image.new("RGB", (SIZE, SIZE), (255, 255, 255))
    flat.paste(icon, mask=icon.split()[3])

    out = OUT_DIR / "app_icon.png"
    flat.save(out)

    # Report the number that actually decides whether this works: how far the subject sits from
    # the background in luminance once shrunk to the size people see it at.
    small = flat.resize((60, 60), Image.LANCZOS)
    lum = lambda p: 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]
    sep = abs(lum(small.getpixel((30, 30))) - lum(small.getpixel((4, 4))))
    print(f"ICON_DONE {out} {flat.size} mode={flat.mode} lumSeparation@60px={sep:.0f}")


if __name__ == "__main__":
    main()
