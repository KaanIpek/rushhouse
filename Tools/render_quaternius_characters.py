"""Render Quaternius FBX animations into Rushhouse's four-direction sprite format.

Run with Blender, for example:
  blender -b --python Tools/render_quaternius_characters.py -- --mode preview
  blender -b --python Tools/render_quaternius_characters.py -- --mode full

The source models are CC0 and stay outside Assets so Unity does not import them
into the player build. The rendered frames are optimized for the existing 2D
restaurant scene while preserving the source rig's real limb animation.
"""

from __future__ import annotations

import argparse
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "SourceArt" / "QuaterniusUltimateCharacters_CC0"
DEFAULT_OUTPUT = PROJECT_ROOT / "Assets" / "Resources" / "Art" / "CharactersRigged"

CHARACTERS = {
    "player": ("Chef_Male.fbx", "employee"),
    "cook": ("Chef_Female.fbx", "employee"),
    "waiter": ("Suit_Male.fbx", "employee"),
    "prepper": ("Worker_Female.fbx", "employee"),
    "customer0": ("Casual_Male.fbx", "customer"),
    "customer1": ("Casual_Female.fbx", "customer"),
    "customer2": ("Casual2_Male.fbx", "customer"),
    "customer3": ("Casual2_Female.fbx", "customer"),
    "customer4": ("Casual3_Male.fbx", "customer"),
    "customer5": ("Casual3_Female.fbx", "customer"),
}

DIRECTIONS = {
    "front": 0.0,
    "left": -math.pi * 0.5,
    "back": math.pi,
    "right": math.pi * 0.5,
}

EMPLOYEE_STATES = {
    "idle": ("Idle", 4, True),
    "walk": ("Walk", 8, True),
    "act": ("PickUp", 8, False),
    "carry": ("Walk_Carry", 4, True),
    "carrywalk": ("Walk_Carry", 8, True),
}

CUSTOMER_STATES = {
    "idle": ("Idle", 4, True),
    "walk": ("Walk", 8, True),
    "sitdown": ("SitDown", 8, False),
    "sit": ("SitDown", 2, False),
    "standup": ("StandUp", 8, False),
    "eat": ("Punch", 6, True),
}

# The same state can use a more readable action for a particular restaurant role.
ROLE_ACTIONS = {
    ("player", "act"): "Punch",
    ("waiter", "act"): "Shoot_OneHanded",
    ("cook", "act"): "Punch",
    ("prepper", "act"): "SwordSlash",
}

ROLE_ACTION_BLEND = {
    "player": 0.58,
    "cook": 0.58,
    "waiter": 0.70,
    "prepper": 0.58,
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("preview", "full"), default="preview")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--character", choices=tuple(CHARACTERS), default=None)
    return parser.parse_args(raw)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.actions,
    ):
        for block in list(datablocks):
            datablocks.remove(block)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render_scene(output_root: Path) -> bpy.types.Scene:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 256
    scene.render.resolution_y = 320
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.use_file_extension = True
    scene.render.filepath = str(output_root)
    scene.render.image_settings.color_depth = "8"
    scene.render.resolution_percentage = 100
    scene.render.fps = 30

    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.color = (0.055, 0.065, 0.08)

    camera_data = bpy.data.cameras.new("RushhouseSpriteCamera")
    camera = bpy.data.objects.new("RushhouseSpriteCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.0, -7.25, 6.35)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 4.15
    camera_data.lens = 50
    look_at(camera, Vector((0.0, 0.0, 1.42)))
    scene.camera = camera

    add_area_light("Key", (-3.8, -4.2, 7.0), 850.0, 5.0, (1.0, 0.87, 0.72))
    add_area_light("Fill", (4.5, -2.0, 5.0), 650.0, 4.0, (0.60, 0.78, 1.0))
    add_area_light("Rim", (0.0, 4.0, 6.7), 900.0, 3.5, (0.70, 1.0, 0.92))
    return scene


def add_area_light(name: str, location, energy: float, size: float, color) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    look_at(obj, Vector((0.0, 0.0, 1.35)))


def import_character(path: Path) -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, automatic_bone_orientation=False)
    imported = [obj for obj in bpy.data.objects if obj not in before]

    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one armature in {path.name}, found {len(armatures)}")
    armature = armatures[0]

    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"} or (obj.type == "MESH" and obj.parent is None):
            bpy.data.objects.remove(obj, do_unlink=True)

    # The legacy FBX exporter stores the visible color correctly but writes
    # Principled alpha as zero. Modern Blender respects that value in Eevee,
    # so normalize it before rendering transparent sprites.
    for obj in imported:
        if obj.type != "MESH" or obj.name not in bpy.data.objects:
            continue
        for slot in obj.material_slots:
            material = slot.material
            if not material:
                continue
            material.diffuse_color[3] = 1.0
            if not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED" and "Alpha" in node.inputs:
                    node.inputs["Alpha"].default_value = 1.0

    armature.rotation_mode = "XYZ"
    armature.rotation_euler = (0.0, 0.0, 0.0)
    armature.location = (0.0, 0.0, 0.0)
    render_root = bpy.data.objects.new("CharacterRenderRoot", None)
    bpy.context.collection.objects.link(render_root)
    armature.parent = render_root
    return armature


def find_action(armature: bpy.types.Object, suffix: str) -> bpy.types.Action:
    suffix = "|" + suffix
    matches = [action for action in bpy.data.actions if action.name.endswith(suffix)]
    if not matches:
        raise RuntimeError(f"Missing animation {suffix} for {armature.name}")
    return matches[0]


def sample_action_frames(action: bpy.types.Action, count: int, loop: bool, state: str) -> list[float]:
    start, end = action.frame_range
    if state == "sit":
        # Hold the completed seated pose with a tiny breathing alternation.
        return [max(start, end - 0.35), end]
    if count <= 1:
        return [start]
    if loop:
        span = max(1.0, end - start)
        return [start + span * i / count for i in range(count)]
    return [start + (end - start) * i / (count - 1) for i in range(count)]


def apply_action(scene: bpy.types.Scene, armature: bpy.types.Object, action: bpy.types.Action, frame: float) -> None:
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action
    whole = int(math.floor(frame))
    scene.frame_set(whole, subframe=frame - whole)
    bpy.context.view_layer.update()


def apply_seated_upper_body_action(
    scene: bpy.types.Scene,
    armature: bpy.types.Object,
    upper_action: bpy.types.Action,
    upper_frame: float,
) -> None:
    """Keep the SitDown end pose below the waist while animating eating arms."""
    apply_action(scene, armature, upper_action, upper_frame)
    upper_pose = {bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones}

    sit_action = find_action(armature, "SitDown")
    apply_action(scene, armature, sit_action, sit_action.frame_range[1])
    lower_names = {
        "Bone", "Body", "Hips", "Foot.L", "Foot.L_end", "Foot.R", "Foot.R_end",
        "UpperLeg.L", "LowerLeg.L", "LowerLeg.L_end",
        "UpperLeg.R", "LowerLeg.R", "LowerLeg.R_end",
        "PoleTarget.L", "PoleTarget.L_end", "PoleTarget.R", "PoleTarget.R_end",
    }
    lower_pose = {
        bone.name: bone.matrix_basis.copy()
        for bone in armature.pose.bones
        if bone.name in lower_names
    }

    armature.animation_data.action = None
    scene.frame_set(0)
    armature.location = (0.0, 0.0, 0.0)
    armature.rotation_euler = (0.0, 0.0, 0.0)
    armature.scale = (1.0, 1.0, 1.0)
    for name, matrix in upper_pose.items():
        armature.pose.bones[name].matrix_basis = matrix
    for name, matrix in lower_pose.items():
        armature.pose.bones[name].matrix_basis = matrix
    bpy.context.view_layer.update()


def apply_blended_work_action(
    scene: bpy.types.Scene,
    armature: bpy.types.Object,
    action: bpy.types.Action,
    frame: float,
    factor: float,
) -> None:
    """Blend dramatic source actions toward idle for compact kitchen work."""
    apply_action(scene, armature, action, frame)
    work_pose = {bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones}

    idle = find_action(armature, "Idle")
    apply_action(scene, armature, idle, idle.frame_range[0])
    idle_pose = {bone.name: bone.matrix_basis.copy() for bone in armature.pose.bones}

    armature.animation_data.action = None
    scene.frame_set(0)
    armature.location = (0.0, 0.0, 0.0)
    armature.rotation_euler = (0.0, 0.0, 0.0)
    armature.scale = (1.0, 1.0, 1.0)
    for name, idle_matrix in idle_pose.items():
        armature.pose.bones[name].matrix_basis = idle_matrix.lerp(work_pose[name], factor)
    bpy.context.view_layer.update()


def render_frame(scene: bpy.types.Scene, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def render_character(
    scene: bpy.types.Scene,
    character: str,
    model_path: Path,
    kind: str,
    output_root: Path,
    preview: bool,
) -> None:
    armature = import_character(model_path)
    states = EMPLOYEE_STATES if kind == "employee" else CUSTOMER_STATES
    directions = DIRECTIONS

    if preview:
        if character not in {"player", "customer0"}:
            return
        if character == "player":
            states = {key: states[key] for key in ("idle", "walk", "act", "carry", "carrywalk")}
        else:
            states = {key: states[key] for key in ("walk", "sitdown", "sit", "standup", "eat")}

    render_root = armature.parent
    for direction, rotation in directions.items():
        render_root.rotation_euler.z = rotation
        for state, (default_action, count, loop) in states.items():
            action_name = ROLE_ACTIONS.get((character, state), default_action)
            action = find_action(armature, action_name)
            frames = sample_action_frames(action, count, loop, state)
            if preview:
                picks = sorted(set((0, len(frames) // 2, len(frames) - 1)))
            else:
                picks = range(len(frames))
            for output_index in picks:
                if state == "eat":
                    apply_seated_upper_body_action(scene, armature, action, frames[output_index])
                elif state == "act" and kind == "employee":
                    apply_blended_work_action(
                        scene,
                        armature,
                        action,
                        frames[output_index],
                        ROLE_ACTION_BLEND.get(character, 0.62),
                    )
                else:
                    apply_action(scene, armature, action, frames[output_index])
                filename = f"{character}_{direction}_{state}_{output_index}.png"
                render_frame(scene, output_root / filename)

    # Remove this import before loading the next FBX, but keep render rig/lights.
    imported_root = {render_root, armature}
    imported_root.update(child for child in armature.children_recursive)
    for obj in imported_root:
        if obj and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def main() -> None:
    args = parse_args()
    output_root = args.output.resolve()
    if args.mode == "preview":
        output_root = PROJECT_ROOT / "_CharacterPreview" / "frames"
    elif output_root.exists() and not args.character:
        # Full generation is deterministic. Clearing prevents stale frame names.
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    clear_scene()
    scene = setup_render_scene(output_root)

    selected = [args.character] if args.character else list(CHARACTERS)
    if args.mode == "preview" and not args.character:
        selected = ["player", "customer0"]
    for character in selected:
        filename, kind = CHARACTERS[character]
        model_path = SOURCE_ROOT / filename
        if not model_path.exists():
            raise FileNotFoundError(model_path)
        print(f"RUSHHOUSE_RENDER character={character} model={filename} mode={args.mode}")
        render_character(scene, character, model_path, kind, output_root, args.mode == "preview")

    print(f"RUSHHOUSE_RENDER_DONE output={output_root}")


if __name__ == "__main__":
    main()
