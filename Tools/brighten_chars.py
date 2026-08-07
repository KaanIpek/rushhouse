"""
Fix the character sprites: (1) shadow-lift the whole frame out of near-black, and
(2) recolor the grey FACE blob (the rig rendered skin as flat grey) to a light skin
tone per character -> mostly fair/white, a couple darker (user: "cogu beyaz, bazisi koyu").
Alpha preserved. Idempotent: originals backed up once, every run processes backup->live.

  py brighten_chars.py --root <Art>
"""
import os, argparse, shutil
import numpy as np
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--root", required=True)
ap.add_argument("--gamma", type=float, default=0.62)
ap.add_argument("--scale", type=float, default=1.22)
ap.add_argument("--add", type=int, default=28)
args = ap.parse_args()

LUT = [max(0, min(255, int(((i / 255) ** args.gamma) * 255 * args.scale + args.add))) for i in range(256)]
FOLDERS = ["CharactersRigged", "CharactersDirectional", "Characters"]

# Skin tone per character. The user asked for light/fair skin across the whole cast
# ("ten renkleri direkt beyaz olsun, ten rengi olsun"), so the two deliberately darker
# entries that used to be here (prepper, customer4) are gone. Small hue differences
# remain so the crowd does not look cloned.
SKIN = {
    "customer0": (255, 234, 218), "customer1": (255, 230, 212), "customer2": (252, 228, 210),
    "customer3": (255, 238, 224), "customer4": (255, 232, 214), "customer5": (250, 226, 206),
    "cook": (255, 234, 216), "player": (255, 236, 220), "waiter": (255, 232, 214),
    "prepper": (255, 236, 218), "customerHappy": (255, 230, 212), "customerNeutral": (252, 226, 206),
    "customerAngry": (255, 232, 214), "customerWalk": (255, 230, 212),
    # Cosmetic outfits are the SAME chef wearing something else, so they take the
    # player's skin tone — otherwise changing jacket colour would change his face.
    "skinCrimson": (255, 236, 220), "skinMidnight": (255, 236, 220),
    "skinGold": (255, 236, 220), "skinMint": (255, 236, 220), "skinNeon": (255, 236, 220),
    "skinTuxedo": (255, 236, 220), "skinKnight": (255, 236, 220), "skinOveralls": (255, 236, 220),
}
DEFAULT_SKIN = (255, 234, 216)


def recolor_face(im, skin, band=0.36, vlo=25, vhi=178, ref=112.0):
    a = np.array(im).astype(np.int32)
    H, W = a.shape[:2]
    r, g, b, al = a[:, :, 0], a[:, :, 1], a[:, :, 2], a[:, :, 3]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    v = (r + g + b) // 3
    yy = np.arange(H)[:, None]
    mask = (al > 60) & ((mx - mn) < 34) & (v >= vlo) & (v <= vhi) & (yy < H * band)   # neutral grey face in the head band
    f = np.clip(v / ref, 0.5, 1.25)[..., None]
    skinned = np.clip(np.array(skin)[None, None, :] * f, 0, 255)
    out = a.copy()
    for ch in range(3):
        out[:, :, ch] = np.where(mask, skinned[:, :, ch], a[:, :, ch])
    return Image.fromarray(out.astype("uint8"), "RGBA")


def _looks_raw(live_dir, sample=12):
    """Raw renders come out of Blender dark (mean opaque luma well under 120); brightened ones
    sit far above it. Sampling a dozen frames is enough to tell the two apart."""
    names = [f for f in os.listdir(live_dir) if f.lower().endswith(".png")][:sample]
    if not names:
        return True
    means = []
    for f in names:
        a = np.array(Image.open(os.path.join(live_dir, f)).convert("RGBA"))
        m = a[:, :, 3] > 200
        if m.any():
            means.append(float(a[:, :, :3][m].mean()))
    return (sum(means) / len(means)) < 120 if means else True


def process(live_dir, backup_dir):
    if not os.path.isdir(live_dir):
        return 0
    if not os.path.isdir(backup_dir):                       # first run: stash the originals
        # DANGER: the live art may ALREADY be brightened -- that is the case on a fresh clone,
        # because Tools/_char_dark_backup is gitignored (143 MB of raw renders) while the
        # processed art in Assets/ is committed. Seeding the backup from processed art and
        # running the LUT again washes every character out. Only auto-seed when the live art
        # is plausibly raw, i.e. dark; otherwise refuse and say why.
        if not _looks_raw(live_dir):
            print("REFUSED " + live_dir + ": no backup and the live art is already bright.")
            print("        Re-render first (render_quaternius_characters.py or render_outfits.py),")
            print("        which writes raw frames, then run this.")
            return 0
        os.makedirs(backup_dir, exist_ok=True)
        for f in os.listdir(live_dir):
            if f.lower().endswith(".png"):
                shutil.copy2(os.path.join(live_dir, f), os.path.join(backup_dir, f))
    # Adopt any frame that exists live but was never backed up (a newly rendered
    # outfit). Without this the backup-driven loop below silently skips it.
    for f in os.listdir(live_dir):
        if f.lower().endswith(".png") and not os.path.exists(os.path.join(backup_dir, f)):
            shutil.copy2(os.path.join(live_dir, f), os.path.join(backup_dir, f))
    n = 0
    for f in os.listdir(backup_dir):
        if not f.lower().endswith(".png"):
            continue
        im = Image.open(os.path.join(backup_dir, f)).convert("RGBA")
        r, g, b, a = im.split()
        im = Image.merge("RGBA", (r.point(LUT), g.point(LUT), b.point(LUT), a))   # shadow lift
        skin = SKIN.get(f.split("_")[0], DEFAULT_SKIN)
        im = recolor_face(im, skin)                                              # grey face -> skin
        im.save(os.path.join(live_dir, f))
        n += 1
    return n


def main():
    total = 0
    for folder in FOLDERS:
        live = os.path.join(args.root, folder)
        backup = os.path.join(os.path.dirname(__file__), "_char_dark_backup", folder)
        c = process(live, backup)
        print(f"{folder}: {c}")
        total += c
    print("BRIGHTEN_DONE", total)


main()
