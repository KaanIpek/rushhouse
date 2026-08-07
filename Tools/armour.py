"""Build plate armour onto an already-imported Quaternius character.

A recolour cannot make armour -- painting the suit steel-grey just produced a
silver suit, because the silhouette still had lapels and a tie. Armour is a
SHAPE, so this adds real geometry: helmet, pauldrons, breastplate, bracers.

The pieces are skinned rather than bone-parented. Each piece gets a single
vertex group named after the bone it should follow, weight 1.0, plus an Armature
modifier pointing at the character rig. That is exactly how the character's own
mesh is bound, so the armour deforms with every animation the renderer plays --
no per-frame fixups, and nothing to keep in sync.

Used by Tools/render_outfits.py; not run on its own.
"""

from __future__ import annotations

import bpy
from mathutils import Vector


def _steel(name: str, colour) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    for node in mat.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            node.inputs["Base Color"].default_value = (colour[0], colour[1], colour[2], 1.0)
            if "Alpha" in node.inputs:
                node.inputs["Alpha"].default_value = 1.0
    mat.diffuse_color = (colour[0], colour[1], colour[2], 1.0)
    return mat


def _bone_world(armature, bone_name: str):
    """Rest-pose head and tail of a bone in world space."""
    bone = armature.data.bones[bone_name]
    m = armature.matrix_world
    return m @ bone.head_local, m @ bone.tail_local


def _mesh_box(armature, bone_name: str):
    """World-space centre and size of the geometry this bone actually drives.

    Sizing armour off BONE lengths failed twice: the Head bone is 0.64 long but the
    head it drives is a 1.1-unit ball centred well above the bone's midpoint, so a
    helmet built from the bone sat inside the skull. Measuring the skinned vertices
    instead means the pieces fit whatever model they are given.
    """
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    found = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH" or not obj.vertex_groups:
            continue
        index_to_name = {g.index: g.name for g in obj.vertex_groups}
        for vert in obj.data.vertices:
            if not vert.groups:
                continue
            top = max(vert.groups, key=lambda g: g.weight)
            if top.weight < 0.5 or index_to_name.get(top.group) != bone_name:
                continue
            w = obj.matrix_world @ vert.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
            found += 1
    if found == 0:
        head, tail = _bone_world(armature, bone_name)
        length = (tail - head).length or 0.1
        return (head + tail) * 0.5, Vector((length, length, length))
    return (lo + hi) * 0.5, (hi - lo)


def _finish(obj, armature, bone_name: str, material):
    """Bind one piece to one bone and hand it the character's rig."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_flat()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    group = obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")

    obj.data.materials.append(material)
    obj.parent = armature
    obj.matrix_parent_inverse = armature.matrix_world.inverted()
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature
    return obj


def add_plate_armour(armature, plate=(0.075, 0.086, 0.115), trim=(0.2158, 0.0802, 0.0012)):
    """Helmet + pauldrons + breastplate + bracers. Returns the created objects.

    Every piece is sized and placed from the MEASURED geometry of the bone it
    covers (see _mesh_box), never from a hand-tuned constant, so the same code
    fits any of the Quaternius characters.

    Colours are LINEAR and deliberately dark: rendered frames are lifted hard by
    Tools/brighten_chars.py, so a mid-grey here comes out white. See the colour
    note at the top of render_outfits.py.
    """
    steel = _steel("ArmourSteel", plate)
    gold = _steel("ArmourTrim", trim)
    dark = _steel("ArmourVisorMat", (0.004, 0.005, 0.007))
    made = []

    # ---- helm: a sphere a touch larger than the skull, so it encloses the head ----
    hc, hs = _mesh_box(armature, "Head")
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=9, location=hc)
    helm = bpy.context.active_object
    helm.name = "ArmourHelm"
    helm.scale = (hs.x * 0.58, hs.y * 0.56, hs.z * 0.58)   # a touch oversize so the model's hair spikes stay inside
    made.append(_finish(helm, armature, "Head", steel))

    # visor band across the face, so it reads as a helm and not a bald steel head
    bpy.ops.mesh.primitive_cube_add(location=hc + Vector((0, -hs.y * 0.34, -hs.z * 0.08)))
    visor = bpy.context.active_object
    visor.name = "ArmourVisor"
    visor.scale = (hs.x * 0.40, hs.y * 0.22, hs.z * 0.11)
    made.append(_finish(visor, armature, "Head", dark))

    # crest along the top: the silhouette cue no suit recolour could ever provide
    # A low fin. It must also be SHALLOW front-to-back: the game camera looks down at
    # 38 degrees, so a deep fore-aft fin projects as a tall vertical pole from the front.
    bpy.ops.mesh.primitive_cube_add(location=hc + Vector((0, 0, hs.z * 0.50)))
    crest = bpy.context.active_object
    crest.name = "ArmourCrest"
    crest.scale = (hs.x * 0.05, hs.y * 0.15, hs.z * 0.09)
    made.append(_finish(crest, armature, "Head", gold))

    # ---- pauldrons + bracers ----
    for side, sign in (("L", 1.0), ("R", -1.0)):
        uc, us = _mesh_box(armature, f"UpperArm.{side}")
        # sit the pauldron at the shoulder end of the upper arm, not its middle
        shoulder = Vector((uc.x - sign * us.x * 0.26, uc.y, uc.z + us.z * 0.12))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=8, location=shoulder)
        pauldron = bpy.context.active_object
        pauldron.name = f"ArmourPauldron{side}"
        pauldron.scale = (us.x * 0.42, us.y * 0.78, us.z * 0.74)
        made.append(_finish(pauldron, armature, f"UpperArm.{side}", steel))

        lc, ls = _mesh_box(armature, f"LowerArm.{side}")
        bpy.ops.mesh.primitive_cube_add(location=lc)
        bracer = bpy.context.active_object
        bracer.name = f"ArmourBracer{side}"
        bracer.scale = (ls.x * 0.46, ls.y * 0.66, ls.z * 0.64)
        made.append(_finish(bracer, armature, f"LowerArm.{side}", steel))

    # ---- breastplate with a gold centre ridge ----
    tc, ts = _mesh_box(armature, "Torso")
    bpy.ops.mesh.primitive_cube_add(location=tc + Vector((0, -ts.y * 0.08, 0)))
    chest = bpy.context.active_object
    chest.name = "ArmourChest"
    chest.scale = (ts.x * 0.64, ts.y * 0.60, ts.z * 0.62)
    made.append(_finish(chest, armature, "Torso", steel))

    bpy.ops.mesh.primitive_cube_add(location=tc + Vector((0, -ts.y * 0.60, 0)))
    ridge = bpy.context.active_object
    ridge.name = "ArmourRidge"
    ridge.scale = (ts.x * 0.09, ts.y * 0.14, ts.z * 0.56)
    made.append(_finish(ridge, armature, "Torso", gold))

    return made
