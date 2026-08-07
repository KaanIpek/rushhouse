"""
Rushhouse 3D kitchen-object generator (Blender 5.1, headless).
Models kitchen equipment (hob, counter, prep, sink, oven, espresso, drink,
provider, table, familyTable, plates, trash, single/dirty plate) in the same
clean stylized look + high-angle ortho as the 3D characters/food.

  blender -b -P object_gen.py -- --mode all --out <dir>
  blender -b -P object_gen.py -- --item hob --out <dir>
"""
import bpy, math, sys, os, argparse
from mathutils import Vector

argv = sys.argv
argv = argv[argv.index("--") + 1:] if "--" in argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--mode", default="all")
ap.add_argument("--item", default="")
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args(argv)
RES = args.res

def mat(name, rgb, rough=0.5, metal=0.0, emit=None, estr=0.0):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = (*emit, 1.0)
        b.inputs["Emission Strength"].default_value = estr
    return m

def am(o, m): o.data.materials.clear(); o.data.materials.append(m)
def smooth(o, subsurf=0, bevel=0.0):
    for p in o.data.polygons: p.use_smooth = True
    if bevel > 0:
        bm = o.modifiers.new("b", 'BEVEL'); bm.width = bevel; bm.segments = 2
    if subsurf > 0:
        s = o.modifiers.new("s", 'SUBSURF'); s.levels = subsurf; s.render_levels = subsurf

def box(name, size, loc, m, bev=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.object; o.name = name; o.scale = (size[0]/2, size[1]/2, size[2]/2); am(o, m)
    if bev > 0: smooth(o, 0, bev)
    return o

def cyl(name, r1, r2, depth, loc, m, verts=28, bev=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
    o = bpy.context.object; o.name = name; am(o, m); smooth(o, 0, bev)
    return o

def sph(name, r, loc, m, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=22, ring_count=14)
    o = bpy.context.object; o.name = name; o.scale = scale; am(o, m); smooth(o, 1)
    return o

# palette
def steel(): return mat("steel", (0.66, 0.68, 0.72), 0.32, 0.85)
def steel_d(): return mat("steel_d", (0.34, 0.35, 0.38), 0.4, 0.7)
def dark(): return mat("dark", (0.12, 0.12, 0.13), 0.5)
def wood(): return mat("wood", (0.52, 0.33, 0.16), 0.6)
def wood_d(): return mat("wood_d", (0.36, 0.22, 0.1), 0.6)
def white(): return mat("white", (0.93, 0.93, 0.95), 0.35)
def chairm(): return mat("chair", (0.32, 0.21, 0.12), 0.6)

# ------------------------------------------------------------------ objects (front = +Y, sit on z=0)
def o_hob():
    box("body", (1.3, 1.0, 0.52), (0, 0, 0.26), steel())
    box("top", (1.24, 0.94, 0.06), (0, 0.02, 0.53), steel_d())
    for x in (-0.32, 0.32):
        for y in (-0.22, 0.24):
            cyl("burner", 0.2, 0.2, 0.05, (x, y, 0.57), dark(), bev=0.01)
            cyl("grate", 0.16, 0.16, 0.02, (x, y, 0.6), mat("grate", (0.2, 0.2, 0.22), 0.5))
    for i, x in enumerate((-0.3, -0.1, 0.1, 0.3)):
        cyl("knob", 0.05, 0.05, 0.06, (x, 0.5, 0.34), dark(), verts=12)

def o_counter():
    box("body", (1.3, 1.0, 0.56), (0, 0, 0.28), steel())
    box("top", (1.3, 1.0, 0.06), (0, 0, 0.57), mat("ctop", (0.76, 0.78, 0.82), 0.28, 0.8))
    box("skirt", (1.28, 0.02, 0.5), (0, 0.5, 0.3), steel_d())
    for x in (-0.32, 0.32):
        box("drawer", (0.5, 0.03, 0.44), (x, 0.51, 0.3), mat("cdr", (0.58, 0.6, 0.64), 0.35, 0.7))
        box("handle", (0.26, 0.05, 0.04), (x, 0.53, 0.42), mat("chand", (0.82, 0.84, 0.87), 0.22, 0.9))

def o_prep():
    o_counter()
    b = box("board", (0.7, 0.6, 0.08), (0, -0.05, 0.62), wood());
    for i, (x, c) in enumerate([(-0.42, (0.85, 0.2, 0.2)), (-0.42, (0.9, 0.8, 0.3)), (0.42, (0.3, 0.7, 0.3))]):
        y = -0.2 + (i % 2) * 0.35
        box("tray", (0.22, 0.22, 0.08), (x, y, 0.62), steel_d())
        sph("veg", 0.09, (x, y, 0.68), mat("veg%d" % i, c))
    box("knife", (0.05, 0.34, 0.03), (0.18, -0.05, 0.67), mat("knife", (0.8, 0.82, 0.85), 0.2, 0.9))

def o_sink():
    box("body", (1.3, 1.0, 0.56), (0, 0, 0.28), steel())
    box("rim", (1.24, 0.94, 0.06), (0, 0.02, 0.57), mat("rim", (0.72, 0.74, 0.78), 0.3, 0.8))
    box("basin", (0.9, 0.66, 0.3), (0, -0.04, 0.45), mat("basin", (0.28, 0.3, 0.34), 0.35, 0.7))
    cyl("water", 0.4, 0.4, 0.02, (0, -0.04, 0.5), mat("water", (0.42, 0.62, 0.72), 0.12, 0.2), verts=22)
    # faucet: base + tall neck + arched spout
    cyl("fbase", 0.08, 0.08, 0.2, (0, 0.36, 0.64), steel(), verts=14)
    cyl("fneck", 0.06, 0.06, 0.3, (0, 0.36, 0.82), steel(), verts=14)
    box("fspout", (0.06, 0.34, 0.06), (0, 0.2, 0.95), steel())
    box("ftap", (0.16, 0.06, 0.06), (0, 0.36, 0.98), mat("ftap", (0.82, 0.84, 0.87), 0.22, 0.9))

def o_oven():
    box("body", (1.3, 1.0, 0.62), (0, 0, 0.31), steel())
    box("door", (1.1, 0.06, 0.5), (0, 0.5, 0.32), steel_d())
    box("window", (0.8, 0.04, 0.32), (0, 0.53, 0.32), mat("glow", (1.0, 0.55, 0.15), 0.3, emit=(1.0, 0.5, 0.12), estr=2.2))
    box("handle", (0.9, 0.08, 0.05), (0, 0.55, 0.6), mat("chr", (0.8, 0.82, 0.85), 0.25, 0.9))
    for x in (-0.4, 0.0, 0.4):
        cyl("dial", 0.05, 0.05, 0.05, (x, 0.5, 0.66), dark(), verts=12)

def o_espresso():
    box("body", (1.0, 0.9, 0.78), (0, 0, 0.39), steel())
    box("head", (0.7, 0.5, 0.14), (0, -0.28, 0.5), steel_d())
    cyl("group", 0.1, 0.1, 0.16, (0, -0.4, 0.42), dark(), verts=16)
    for x in (-0.16, 0.16):
        cyl("cup", 0.1, 0.09, 0.12, (x, -0.4, 0.3), white(), verts=16)
    box("wand", (0.04, 0.04, 0.3), (0.34, -0.34, 0.5), steel())
    box("panel", (0.9, 0.04, 0.4), (0, 0.45, 0.5), mat("epanel", (0.2, 0.22, 0.26), 0.4))

def o_drink():
    box("body", (1.1, 0.9, 0.5), (0, 0, 0.25), steel_d())
    for i, c in enumerate([(0.85, 0.2, 0.16), (0.95, 0.55, 0.15), (0.35, 0.7, 0.3)]):
        x = -0.34 + i * 0.34
        cyl("tank", 0.15, 0.15, 0.5, (x, -0.02, 0.72), mat("dr%d" % i, c, 0.2))
        box("nozzle", (0.06, 0.06, 0.1), (x, -0.24, 0.5), dark())

def o_provider():
    box("shelf", (1.24, 0.94, 0.5), (0, 0, 0.25), steel_d())
    cols = [(0.85, 0.2, 0.16), (0.5, 0.75, 0.28), (0.95, 0.78, 0.2), (0.7, 0.28, 0.22), (0.9, 0.5, 0.2), (0.6, 0.4, 0.2)]
    k = 0
    for r in (0.2, -0.18):
        for c in (-0.38, 0.0, 0.38):
            box("tray", (0.3, 0.3, 0.1), (c, r, 0.52), dark())
            sph("cont", 0.11, (c, r, 0.58), mat("pc%d" % k, cols[k % len(cols)]), (1, 1, 0.7)); k += 1

def _chair(x, y, ang):
    import math as _m
    seat = box("seat", (0.34, 0.34, 0.08), (x, y, 0.34), chairm())
    seat.rotation_euler = (0, 0, ang)
    bk = box("back", (0.34, 0.07, 0.3), (x - 0.16*_m.sin(ang), y + 0.16*_m.cos(ang), 0.5), wood_d())
    bk.rotation_euler = (0, 0, ang)

def o_table():
    cyl("top", 0.6, 0.58, 0.1, (0, 0, 0.5), wood(), bev=0.02)
    cyl("leg", 0.1, 0.1, 0.5, (0, 0, 0.25), wood_d(), verts=12)
    _chair(0, -0.72, 0); _chair(0, 0.72, math.pi)

def o_familyTable():
    cyl("top", 0.72, 0.7, 0.1, (0, 0, 0.5), wood(), bev=0.02)
    cyl("leg", 0.12, 0.12, 0.5, (0, 0, 0.25), wood_d(), verts=12)
    import math as _m
    for i in range(4):
        a = i * (_m.pi/2)
        _chair(0.86*_m.cos(a), 0.86*_m.sin(a), a + _m.pi/2)

def o_plates():
    for i in range(6):
        cyl("pl", 0.44 - i*0.005, 0.44 - i*0.005, 0.035, (0, 0, 0.03 + i*0.045), white(), bev=0.01)

def o_singlePlate():
    p = cyl("pl", 0.5, 0.48, 0.06, (0, 0, 0.03), white(), bev=0.02); smooth(p, 1)
    cyl("well", 0.34, 0.34, 0.03, (0, 0, 0.05), mat("plw", (0.85, 0.85, 0.88), 0.35))

def o_dirtyPlate():
    o_singlePlate()
    for (dx, dy, c) in [(-0.08, 0.06, (0.4, 0.26, 0.14)), (0.1, -0.05, (0.5, 0.3, 0.16)), (0.02, 0.12, (0.35, 0.5, 0.22))]:
        sph("scrap", 0.06, (dx, dy, 0.08), mat("scr%s" % str(c), c), (1, 1, 0.5))

def o_trash():
    cyl("bin", 0.34, 0.46, 0.74, (0, 0, 0.37), mat("bin", (0.16, 0.17, 0.19), 0.5), verts=22)
    cyl("rim", 0.48, 0.48, 0.07, (0, 0, 0.74), steel_d(), verts=22)
    cyl("hole", 0.4, 0.4, 0.05, (0, 0, 0.745), mat("void", (0.03, 0.03, 0.04)), verts=22)
    sph("bag", 0.36, (0, 0, 0.72), mat("bag", (0.1, 0.1, 0.12), 0.6), (1, 1, 0.4))

def o_dtable():
    top = mat("dt_top", (0.40, 0.24, 0.12), 0.42)
    topL = mat("dt_topL", (0.52, 0.33, 0.17), 0.42)
    leg = mat("dt_leg", (0.26, 0.15, 0.07), 0.5)
    box("apron", (1.18, 0.86, 0.16), (0, 0, 0.5), leg, bev=0.02)
    for x in (-0.52, 0.52):
        for y in (-0.38, 0.38):
            box("leg", (0.11, 0.11, 0.58), (x, y, 0.29), leg, bev=0.02)
    box("top", (1.34, 1.02, 0.11), (0, 0, 0.63), top, bev=0.03)
    box("topsheen", (1.16, 0.86, 0.02), (0, -0.03, 0.69), topL)   # lit top face

def o_dchair():
    frame = mat("dc_fr", (0.30, 0.18, 0.09), 0.5)
    cush = mat("dc_cu", (0.58, 0.30, 0.18), 0.55)
    cushL = mat("dc_cuL", (0.72, 0.40, 0.24), 0.55)
    for x in (-0.18, 0.18):
        for y in (-0.18, 0.18):
            box("leg", (0.08, 0.08, 0.44), (x, y, 0.22), frame, bev=0.01)
    box("seat", (0.52, 0.52, 0.1), (0, 0, 0.47), frame, bev=0.02)
    box("cushion", (0.46, 0.46, 0.08), (0, 0, 0.54), cush, bev=0.03)
    box("cushsheen", (0.4, 0.4, 0.02), (0, -0.02, 0.59), cushL)
    box("backpost_l", (0.07, 0.07, 0.5), (-0.22, 0.24, 0.7), frame, bev=0.01)
    box("backpost_r", (0.07, 0.07, 0.5), (0.22, 0.24, 0.7), frame, bev=0.01)
    box("backrest", (0.5, 0.08, 0.26), (0, 0.24, 0.82), cush, bev=0.02)

def o_crate():
    wd = mat("cw", (0.60, 0.40, 0.20), 0.62)
    wdd = mat("cwd", (0.42, 0.26, 0.12), 0.62)
    h = 0.44
    box("bottom", (1.06, 0.94, 0.06), (0, 0, 0.03), wdd)
    box("wfront", (1.1, 0.1, h), (0, -0.46, h / 2), wd, bev=0.015)
    box("wback", (1.1, 0.1, h), (0, 0.46, h / 2), wdd, bev=0.015)
    for sx in (-0.52, 0.52):
        box("wside", (0.1, 0.98, h), (sx, 0, h / 2), wd, bev=0.015)
    for x in (-0.52, 0.52):
        for y in (-0.46, 0.46):
            box("post", (0.14, 0.14, h + 0.06), (x, y, (h + 0.06) / 2), wdd, bev=0.02)
    box("seam", (1.02, 0.11, 0.02), (0, -0.46, h * 0.55), wdd)
    box("seam2", (1.02, 0.11, 0.02), (0, -0.46, h * 0.28), wdd)

def o_bin():
    st = steel(); std = steel_d()
    h = 0.4
    box("body", (1.02, 0.86, h), (0, 0, h / 2), st, bev=0.04)
    box("rim", (1.12, 0.96, 0.08), (0, 0, h), mat("brim", (0.82, 0.84, 0.88), 0.26, 0.92), bev=0.02)
    box("inner", (0.86, 0.7, 0.3), (0, 0, h * 0.62), mat("binn", (0.36, 0.4, 0.46), 0.4, 0.5))
    cyl("frost", 0.4, 0.4, 0.02, (0, 0, h * 0.78), mat("frost", (0.86, 0.93, 1.0), 0.2), verts=24)

OBJECTS = {
    "dtable": o_dtable, "dchair": o_dchair,
    "crate": o_crate, "bin": o_bin,
    "hob": o_hob, "counter": o_counter, "prep": o_prep, "sink": o_sink, "oven": o_oven,
    "espresso": o_espresso, "drink": o_drink, "provider": o_provider,
    "table": o_table, "familyTable": o_familyTable, "plates": o_plates,
    "trash": o_trash, "singlePlate": o_singlePlate, "dirtyPlate": o_dirtyPlate,
}

# ------------------------------------------------------------------ scene / render
def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.film_transparent = True
    sc.render.resolution_x = RES; sc.render.resolution_y = RES
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    try: sc.eevee.taa_render_samples = 48
    except Exception: pass
    tgt = bpy.data.objects.new("t", None); sc.collection.objects.link(tgt); tgt.location = (0, 0, 0.32)
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 2.15
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam)
    elev = math.radians(50); D = 12
    cam.location = (0, D*math.cos(elev), 0.32 + D*math.sin(elev))
    c = cam.constraints.new('TRACK_TO'); c.target = tgt; c.track_axis = 'TRACK_NEGATIVE_Z'; c.up_axis = 'UP_Y'
    sc.camera = cam
    def sun(rot, e, col=(1, 1, 1)):
        d = bpy.data.lights.new("s", 'SUN'); d.energy = e; d.color = col
        try: d.angle = math.radians(18)
        except Exception: pass
        o = bpy.data.objects.new("s", d); sc.collection.objects.link(o); o.rotation_euler = [math.radians(a) for a in rot]
    sun((52, 8, 28), 4.2, (1, 0.97, 0.9)); sun((60, 0, -150), 1.7, (0.85, 0.9, 1)); sun((-26, 0, 178), 2.2, (1, 0.96, 0.88))
    w = bpy.data.worlds.new("W"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.43, 0.46, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.72
    sc.world = w
    try: sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
    except Exception: pass

def render_one(name, builder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    builder(); setup_scene()
    bpy.context.scene.render.filepath = os.path.join(args.out, name + ".png")
    bpy.ops.render.render(write_still=True)

def main():
    os.makedirs(args.out, exist_ok=True)
    if args.item:
        render_one(args.item, OBJECTS[args.item])
    else:
        for n, b in OBJECTS.items(): render_one(n, b)
    print("OBJGEN_DONE")

main()
