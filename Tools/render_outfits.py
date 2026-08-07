"""Render cosmetic chef outfits as extra player sprite sets.

The Quaternius chef model keeps its parts on separate materials (Skin, Face,
Hair, Shirt, Clothes, DarkClothes, Pants, Band), so an outfit is just a palette
swap on the cloth materials plus, optionally, a different base model. That is a
real visual change -- not a tint over the whole sprite, which would recolour the
chef's face along with the jacket.

Runs through the existing renderer so every outfit lands on exactly the same
frames, camera and lighting as `player_*`; only the material colours differ.

  blender -b --python Tools/render_outfits.py -- --outfit crimson
  blender -b --python Tools/render_outfits.py -- --all

Output: Assets/Resources/Art/CharactersRigged/<outfit>_<dir>_<state>_<n>.png
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import render_quaternius_characters as rq
import armour as armour_kit

# WHICH MATERIAL IS WHICH, measured not guessed (probe: paint each material a primary
# and look). On BOTH chef models only three materials are visible from the game camera:
#   Clothes     -> the jacket (torso + arms)
#   DarkClothes -> the trousers
#   Hair        -> hair, or the white toque on the male chef
# Shirt / Pants / Band / Hat exist in the FBX but are never on screen, so colouring them
# does nothing. An earlier pass wasted five outfits discovering that.
#
# WHY THESE COLOURS LOOK ALMOST BLACK: rendered frames go through Tools/brighten_chars.py,
# whose LUT (gamma .62, x1.22, +28) is very aggressive -- raw 40 becomes 126, raw 160 and
# above clips to 255. Two consequences drive every value below:
#   1. Anything mid-bright washes out to pastel. The first attempt used 0.72 red and got
#      salmon pink.
#   2. Saturation only survives if the channels you want dark are actually ZERO, because
#      the +28 term lifts every channel equally and greys the hue.
# So each colour here is solved backwards from the wanted on-screen result:
#   target_after_LUT -> required raw sRGB -> linear = ((v/255 + .055)/1.055) ** 2.4
OUTFITS = {
    # jacket -> ~(205,50,45) crimson, trousers -> ~(130,30,28)
    "skinCrimson": dict(model="Chef_Male.fbx", colors={
        "Clothes": (0.127, 0.0006, 0.0006), "DarkClothes": (0.0216, 0.0, 0.0),
    }),
    # jacket -> ~(45,60,150) navy, trousers near black. Reds must be 0 or it goes powder blue.
    "skinMidnight": dict(model="Chef_Male.fbx", colors={
        "Clothes": (0.0006, 0.0021, 0.0384), "DarkClothes": (0.0, 0.0003, 0.0049),
    }),
    # jacket -> ~(30,175,140) teal
    "skinMint": dict(model="Chef_Female.fbx", colors={
        "Clothes": (0.0, 0.0722, 0.0302), "DarkClothes": (0.0015, 0.0036, 0.003),
    }),
    # jacket -> ~(235,180,40) gold; blue channel at 0 or it turns cream
    "skinGold": dict(model="Chef_Male.fbx", colors={
        "Clothes": (0.2308, 0.0802, 0.0003), "DarkClothes": (0.0042, 0.0024, 0.0006),
    }),
    # jacket magenta, hair cyan -- the one outfit that deliberately recolours hair
    "skinNeon": dict(model="Chef_Female.fbx", colors={
        "Clothes": (0.1946, 0.0006, 0.0722), "DarkClothes": (0.003, 0.0, 0.0024),
        "Hair": (0.0, 0.1022, 0.1557),
    }),

    # ---- COSTUMES: a different BASE MODEL, so the silhouette changes, not just the palette.
    # Both models below carry the same animation set as the chefs (Idle/Walk/PickUp/Walk_Carry),
    # which is why they drop into the existing renderer untouched. Their material names differ
    # again -- measured, not guessed:
    #   Suit_Male     Black = jacket + trousers, Shirt = dress shirt, Details = tie, Belt = belt
    #   Worker_Female Hat = hard hat, Vest = bib/overall, Shirt = tee, Pants = trousers
    "skinTuxedo": dict(model="Suit_Male.fbx", colors={
        "Black": (0.0015, 0.0016, 0.0022),      # near-black suit
        "Shirt": (0.30, 0.30, 0.30),            # crisp white dress shirt
        "Details": (0.1022, 0.0006, 0.0006),    # deep red tie
        "Belt": (0.0015, 0.0015, 0.0015),
    }),
    # Real plate: helmet, pauldrons, breastplate and bracers are GEOMETRY built onto the
    # rig by Tools/armour.py, not a palette. Recolouring a suit steel-grey was tried first
    # and only ever looked like a silver suit -- armour is a silhouette, not a colour.
    # The body underneath goes near-black so it reads as the gambeson below the plate,
    # and Hair goes steel so any spike poking through the helm reads as part of it.
    "skinKnight": dict(model="Chef_Male.fbx", armour=True, colors={
        "Clothes": (0.004, 0.005, 0.008),
        "DarkClothes": (0.004, 0.005, 0.008),
        "Hair": (0.075, 0.086, 0.115),
    }),
    "skinForeman": dict(model="Worker_Female.fbx", colors={
        "Hat": (0.262, 0.127, 0.0006),          # safety yellow hard hat
        "Vest": (0.262, 0.0097, 0.0003),        # hi-viz orange bib
        "Shirt": (0.30, 0.30, 0.30),
        "Pants": (0.003, 0.007, 0.0384),        # denim
    }),
    "skinFire": dict(model="Worker_Female.fbx", colors={
        "Hat": (0.188, 0.0006, 0.0006),         # fire-red helmet
        "Vest": (0.0042, 0.0046, 0.0052),       # dark turnout coat
        "Shirt": (0.262, 0.127, 0.0006),        # hi-viz stripe
        "Pants": (0.003, 0.0035, 0.004),
    }),
}


def recolour(colors: dict) -> int:
    """Repaint the imported model's cloth materials. Returns how many hit, so a
    renamed material in a future model pack fails loudly instead of silently
    producing an outfit identical to the default one."""
    hits = 0
    for material in bpy.data.materials:
        target = colors.get(material.name)
        if not target:
            continue
        rgba = (target[0], target[1], target[2], 1.0)
        material.diffuse_color = rgba
        if material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    node.inputs["Base Color"].default_value = rgba
        hits += 1
    return hits


def main() -> None:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--outfit", choices=tuple(OUTFITS))
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--output", type=Path, default=rq.DEFAULT_OUTPUT)
    args = parser.parse_args(raw)

    names = list(OUTFITS) if args.all or not args.outfit else [args.outfit]
    output_root = args.output.resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    rq.clear_scene()
    scene = rq.setup_render_scene(output_root)

    for name in names:
        spec = OUTFITS[name]
        model_path = rq.SOURCE_ROOT / spec["model"]
        if not model_path.exists():
            raise FileNotFoundError(model_path)
        print(f"OUTFIT_RENDER {name} model={spec['model']}", flush=True)

        # Import, repaint, then hand the same armature to the stock renderer.
        armature = rq.import_character(model_path)
        hits = recolour(spec["colors"])
        if hits < len(spec["colors"]):
            raise RuntimeError(f"{name}: only {hits}/{len(spec['colors'])} materials matched")
        extra = armour_kit.add_plate_armour(armature) if spec.get("armour") else []
        if spec.get("armour"):
            print(f"  armour pieces: {len(extra)}", flush=True)

        render_root = armature.parent
        for direction, rotation in rq.DIRECTIONS.items():
            render_root.rotation_euler.z = rotation
            for state, (default_action, count, loop) in rq.EMPLOYEE_STATES.items():
                action_name = rq.ROLE_ACTIONS.get(("player", state), default_action)
                action = rq.find_action(armature, action_name)
                frames = rq.sample_action_frames(action, count, loop, state)
                for i in range(len(frames)):
                    if state == "act":
                        rq.apply_blended_work_action(scene, armature, action, frames[i],
                                                     rq.ROLE_ACTION_BLEND["player"])
                    else:
                        rq.apply_action(scene, armature, action, frames[i])
                    rq.render_frame(scene, output_root / f"{name}_{direction}_{state}_{i}.png")

        doomed = {render_root, armature}
        doomed.update(child for child in armature.children_recursive)
        doomed.update(extra)      # armour is parented to the armature, but be explicit
        for obj in doomed:
            if obj and obj.name in bpy.data.objects:
                bpy.data.objects.remove(obj, do_unlink=True)
        for action in list(bpy.data.actions):
            bpy.data.actions.remove(action)
        # Materials come back with the FBX, so drop them too or the next outfit
        # inherits this one's palette.
        for material in list(bpy.data.materials):
            bpy.data.materials.remove(material)

    print(f"OUTFIT_RENDER_DONE {','.join(names)}", flush=True)


if __name__ == "__main__":
    main()
