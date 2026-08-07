"""
Render Rodin prop FBXs to game-angle sprite PNGs (transparent bg, ~48deg top-down
ortho, front-facing to match the characters). BATCH: one Blender session over a JSON
manifest so we pay the startup once.

  blender -b -P render_props.py -- <manifest.json>
manifest = [ {"dir": "<prop folder w/ base.fbx>", "out": "<out.png>", "elev": 48, "azimuth": 0}, ... ]
"""
import bpy, sys, os, math, json
from mathutils import Vector

manifest_path = sys.argv[sys.argv.index("--") + 1]
entries = json.load(open(manifest_path, encoding="utf-8"))

for mod in ("io_scene_fbx",):
    try: bpy.ops.preferences.addon_enable(module=mod)
    except Exception: pass


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def render_one(prop_dir, out, elev=48.0, azimuth=0.0, res=512):
    clear_scene()
    scene = bpy.context.scene
    fbx = os.path.join(prop_dir, "base.fbx")
    objf = os.path.join(prop_dir, "base.obj")
    if os.path.exists(fbx):
        bpy.ops.import_scene.fbx(filepath=fbx)
    elif os.path.exists(objf):
        try: bpy.ops.wm.obj_import(filepath=objf)
        except Exception: bpy.ops.import_scene.obj(filepath=objf)
    else:
        print("SKIP_NOFILE", prop_dir); return False
    meshes = [o for o in scene.objects if o.type == "MESH"]
    if not meshes:
        print("SKIP_NOMESH", prop_dir); return False
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1: bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active

    diff = os.path.join(prop_dir, "texture_diffuse.png")
    mat = bpy.data.materials.new("prop"); mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.62
    if "Metallic" in bsdf.inputs: bsdf.inputs["Metallic"].default_value = 0.0
    if os.path.exists(diff):
        tx = mat.node_tree.nodes.new("ShaderNodeTexImage"); tx.image = bpy.data.images.load(diff)
        mat.node_tree.links.new(bsdf.inputs["Base Color"], tx.outputs["Color"])
    obj.data.materials.clear(); obj.data.materials.append(mat)

    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    obj.location = (0, 0, 0)
    d = obj.dimensions
    obj.scale = (2.0 / max(d.x, d.y, d.z, 1e-4),) * 3
    obj.rotation_euler.z = math.radians(azimuth)
    bpy.context.view_layer.update()
    zmin = min((obj.matrix_world @ Vector(c)).z for c in obj.bound_box)
    obj.location.z -= zmin
    bpy.context.view_layer.update()
    cx = sum((obj.matrix_world @ Vector(c)).x for c in obj.bound_box) / 8.0
    cy = sum((obj.matrix_world @ Vector(c)).y for c in obj.bound_box) / 8.0
    cz = sum((obj.matrix_world @ Vector(c)).z for c in obj.bound_box) / 8.0

    tgt = bpy.data.objects.new("tgt", None); scene.collection.objects.link(tgt); tgt.location = (cx, cy, cz)
    cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = 2.7
    cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam)
    er = math.radians(elev); D = 12.0
    cam.location = (cx, cy - D * math.cos(er), cz + D * math.sin(er))
    con = cam.constraints.new("TRACK_TO"); con.target = tgt; con.track_axis = "TRACK_NEGATIVE_Z"; con.up_axis = "UP_Y"
    scene.camera = cam

    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN")); scene.collection.objects.link(key)
    key.data.energy = 3.2; key.data.color = (1.0, 0.96, 0.9); key.rotation_euler = (math.radians(52), 0, math.radians(38))
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN")); scene.collection.objects.link(fill)
    fill.data.energy = 1.1; fill.data.color = (0.85, 0.9, 1.0); fill.rotation_euler = (math.radians(60), 0, math.radians(-135))
    world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.57, 0.6, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.45

    try: scene.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        try: scene.render.engine = "BLENDER_EEVEE"
        except Exception: scene.render.engine = "CYCLES"
    scene.render.film_transparent = True
    scene.render.resolution_x = res; scene.render.resolution_y = res
    scene.render.image_settings.file_format = "PNG"; scene.render.image_settings.color_mode = "RGBA"
    try: scene.view_settings.view_transform = "Standard"
    except Exception: pass
    os.makedirs(os.path.dirname(out), exist_ok=True)
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    return True


ok = 0
for e in entries:
    try:
        if render_one(e["dir"], e["out"], e.get("elev", 48.0), e.get("azimuth", 0.0), e.get("res", 512)):
            ok += 1
            print("OK", os.path.basename(e["out"]))
    except Exception as ex:
        print("ERR", e.get("out"), repr(ex))
print(f"BATCH_DONE {ok}/{len(entries)}")
