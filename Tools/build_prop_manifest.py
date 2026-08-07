"""Backup current Art sprites, then emit a Blender render manifest: one front render per
prop folder to a staging dir (+ 4 chair rotations). Mapping to game names happens later."""
import os, json, shutil

PROPS = r"C:\Users\RLD_R\Downloads\rushhouse props"
ART = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\outputs\rushhouse-unity\Assets\Resources\Art"
STAGE = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\work\props_rendered"
BACKUP = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\outputs\rushhouse-unity\Tools\_pre3d_art_backup"
MANIFEST = os.path.join(os.path.dirname(__file__), "prop_manifest.json")

# one-time backup of the folders we may overwrite
for sub in ("Objects", "Foods", "FinalDishes"):
    dst = os.path.join(BACKUP, sub)
    if not os.path.isdir(dst):
        shutil.copytree(os.path.join(ART, sub), dst)
        print("backed up", sub)

os.makedirs(STAGE, exist_ok=True)
folders = [d for d in os.listdir(PROPS)
           if os.path.isdir(os.path.join(PROPS, d)) and os.path.exists(os.path.join(PROPS, d, "base.fbx"))]

entries = []
for d in folders:
    entries.append({"dir": os.path.join(PROPS, d), "out": os.path.join(STAGE, d + ".png"), "elev": 48, "azimuth": 0})

# chair: 4 rotations for the 4 seat directions
if os.path.exists(os.path.join(PROPS, "chair", "base.fbx")):
    for i in range(4):
        entries.append({"dir": os.path.join(PROPS, "chair"),
                        "out": os.path.join(STAGE, f"chair_{i}.png"), "elev": 48, "azimuth": 90 * i})

json.dump(entries, open(MANIFEST, "w", encoding="utf-8"))
print("MANIFEST", MANIFEST, "entries", len(entries), "folders", len(folders))
