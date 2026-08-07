"""
Rushhouse 3D food generator (Blender 5.1, headless).

Models ingredients + final dishes from primitives in a clean stylized look that
matches the 3D characters (EEVEE, Standard view transform, high-angle ortho),
rendered with alpha into Art/Foods, Art/FinalDishes, Art/Carry.

Run:
  blender -b -P food_gen.py -- --mode ingredients --out <dir>
  blender -b -P food_gen.py -- --mode dishes --out <dir>
  blender -b -P food_gen.py -- --item bun --out <dir>
"""
import bpy, math, sys, os, argparse
from mathutils import Vector, Euler

argv = sys.argv
argv = argv[argv.index("--") + 1:] if "--" in argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--mode", default="ingredients")   # ingredients | dishes | one
ap.add_argument("--item", default="")
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=320)
args = ap.parse_args(argv)

# ------------------------------------------------------------------ helpers
def mat(name, rgb, rough=0.5, metal=0.0):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m

def am(o, m): o.data.materials.clear(); o.data.materials.append(m)

def smooth(o, subsurf=1, bevel=0.0):
    for p in o.data.polygons: p.use_smooth = True
    if bevel > 0:
        bm = o.modifiers.new("bev", 'BEVEL'); bm.width = bevel; bm.segments = 2
    if subsurf > 0:
        s = o.modifiers.new("sub", 'SUBSURF'); s.levels = subsurf; s.render_levels = subsurf

def cyl(name, r1, r2, depth, loc, m, verts=28):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
    o = bpy.context.object; o.name = name; am(o, m); return o

def sph(name, r, loc, m, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=28, ring_count=18)
    o = bpy.context.object; o.name = name; o.scale = scale; am(o, m); return o

def box(name, size, loc, m):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.object; o.name = name; o.scale = (size[0]/2, size[1]/2, size[2]/2); am(o, m); return o

def torus(name, R, r, loc, m):
    bpy.ops.mesh.primitive_torus_add(location=loc, major_radius=R, minor_radius=r)
    o = bpy.context.object; o.name = name; am(o, m)
    for p in o.data.polygons: p.use_smooth = True
    return o

# ------------------------------------------------------------------ palette
C = dict(
    bun=(0.74, 0.44, 0.17), bun_bot=(0.64, 0.38, 0.16), sesame=(0.97, 0.92, 0.74),
    patty_raw=(0.72, 0.30, 0.28), patty=(0.34, 0.20, 0.12),
    lettuce=(0.48, 0.76, 0.28), tomato=(0.84, 0.18, 0.15), tomato_in=(0.95, 0.5, 0.42),
    cheese=(0.98, 0.75, 0.20), pcheese=(0.93, 0.70, 0.30), sauce=(0.88, 0.36, 0.16),
    dough=(0.92, 0.83, 0.66), baked=(0.84, 0.62, 0.34),
    coffee=(0.20, 0.12, 0.07), milk=(0.97, 0.97, 0.93), drink=(0.18, 0.55, 0.92),
    ice=(0.7, 0.88, 1.0), cup=(0.95, 0.95, 0.96), dish=(0.9, 0.9, 0.93),
    foam=(0.96, 0.9, 0.78), grill=(0.24, 0.15, 0.1),
    saus_raw=(0.80, 0.44, 0.42), saus_ck=(0.56, 0.30, 0.17), saus_char=(0.30, 0.16, 0.08), onion=(0.95, 0.93, 0.87),
    tortilla=(0.80, 0.55, 0.28), tort_edge=(0.62, 0.40, 0.19), tort_char=(0.42, 0.26, 0.12),
    beef=(0.44, 0.24, 0.13), beef_hi=(0.56, 0.32, 0.18), jalapeno=(0.30, 0.55, 0.20), cheese_melt=(0.96, 0.72, 0.24),
)
def M(k, **kw): return mat(k, C[k], **kw)

# ------------------------------------------------------------------ ingredient builders (feet on z=0)
def i_bun():
    d = sph("bun", 0.5, (0, 0, 0.02), M("bun"), (1, 1, 0.62)); smooth(d, 1)
    import math as _m
    for i in range(7):
        a = i * (_m.tau / 7); r = 0.24 if i else 0
        sph("ses", 0.05, (r*_m.cos(a), r*_m.sin(a), 0.30), M("sesame"), (1, 1, 0.5))

def i_pattyRaw():
    p = cyl("pr", 0.46, 0.44, 0.17, (0, 0, 0.09), M("patty_raw")); smooth(p, 1, 0.03)

def i_pattyCooked():
    p = cyl("pc", 0.46, 0.44, 0.17, (0, 0, 0.09), M("patty")); smooth(p, 1, 0.03)
    for gx in (-0.16, 0.0, 0.16):
        box("g", (0.04, 0.6, 0.02), (gx, 0, 0.18), M("grill"))

def i_lettuce():
    base = sph("l", 0.42, (0, 0, 0.08), M("lettuce"), (1, 1, 0.42)); smooth(base, 1)
    import math as _m
    for i in range(9):
        a = i * (_m.tau / 9)
        r = sph("lf", 0.17, (0.36*_m.cos(a), 0.36*_m.sin(a), 0.13), M("lettuce"), (1, 1, 0.5)); smooth(r, 1)

def i_tomato():
    d = cyl("t", 0.46, 0.46, 0.12, (0, 0, 0.06), M("tomato")); smooth(d, 1, 0.02)
    cyl("ti", 0.3, 0.3, 0.13, (0, 0, 0.07), M("tomato_in"))
    import math as _m
    for i in range(5):
        a = i * (_m.tau / 5)
        sph("seed", 0.03, (0.16*_m.cos(a), 0.16*_m.sin(a), 0.13), M("tomato"))

def i_cheese():
    s = box("ch", (0.94, 0.94, 0.09), (0, 0, 0.06), M("cheese")); smooth(s, 0, 0.04)
    s.rotation_euler = (0, 0, math.radians(10))

def _bowl(mkey):
    b = cyl("bowl", 0.52, 0.4, 0.2, (0, 0, 0.1), M("dish")); smooth(b, 1)
    return b

def i_sauce():
    _bowl("dish")
    s = sph("sc", 0.36, (0, 0, 0.17), M("sauce"), (1, 1, 0.42)); smooth(s, 1)

def i_dough():
    d = sph("do", 0.5, (0, 0, 0.02), M("dough"), (1, 1, 0.7)); smooth(d, 1)

def i_doughBaked():
    d = cyl("db", 0.56, 0.52, 0.13, (0, 0, 0.065), M("baked")); smooth(d, 1, 0.03)

def _cup(fill_key, fillz=0.4, foam=False):
    cup = cyl("cup", 0.34, 0.4, 0.52, (0, 0, 0.26), M("cup")); smooth(cup, 1)
    fk = M(fill_key)
    f = cyl("fill", 0.34, 0.34, 0.06, (0, 0, fillz), fk); smooth(f, 1)
    if foam:
        cyl("foam", 0.33, 0.33, 0.05, (0, 0, fillz + 0.05), M("foam"))
    torus("handle", 0.16, 0.045, (0.44, 0, 0.28), M("cup"))
    return cup

def i_coffee():
    _cup("coffee", 0.47)

def i_milk():
    g = cyl("mk", 0.32, 0.36, 0.54, (0, 0, 0.27), M("milk")); smooth(g, 1)

def i_drink():
    cup = cyl("dc", 0.34, 0.4, 0.54, (0, 0, 0.27), mat("cup_clear", C["cup"], rough=0.15))
    smooth(cup, 1)
    cyl("df", 0.33, 0.33, 0.42, (0, 0, 0.24), M("drink"))
    for (dx, dy) in [(-0.12, 0.05), (0.1, -0.08), (0.02, 0.12)]:
        c = box("ice", (0.12, 0.12, 0.12), (dx, dy, 0.5), M("ice")); c.rotation_euler = (0.5, 0.4, 0.2)

def _sausage(mkey, char=False):
    m = M(mkey)
    s = cyl("sg", 0.17, 0.17, 0.66, (0, 0, 0.17), m); s.rotation_euler = (0, math.radians(90), 0); smooth(s, 1)
    for e in (-0.31, 0.31): sph("cap", 0.17, (e, 0, 0.17), m)
    if char:
        for gx in (-0.18, 0.0, 0.18):
            box("ch", (0.03, 0.3, 0.02), (gx, 0, 0.34), M("saus_char"))

def i_sausageRaw(): _sausage("saus_raw")
def i_sausageCooked(): _sausage("saus_ck", True)

def i_onion():
    m = M("onion")
    for (dx, dy) in [(-0.22, 0.1), (0.12, -0.16), (-0.06, 0.22), (0.24, 0.12), (0.02, 0.0), (-0.16, -0.12), (0.18, -0.02)]:
        b = box("on", (0.17, 0.17, 0.1), (dx, dy, 0.06), m); b.rotation_euler = (0, 0, dx * 4); smooth(b, 0, 0.02)

def i_tortilla():
    d = cyl("to", 0.52, 0.52, 0.05, (0, 0, 0.03), M("tortilla")); smooth(d, 1, 0.02)
    for (dx, dy) in [(-0.22, 0.12), (0.16, -0.16), (0.06, 0.24), (-0.1, -0.2)]:
        sph("sp", 0.055, (dx, dy, 0.055), M("tort_char"), (1, 1, 0.25))

INGREDIENTS = {
    "bun": i_bun, "pattyRaw": i_pattyRaw, "pattyCooked": i_pattyCooked,
    "lettuce": i_lettuce, "lettuceReady": i_lettuce, "tomato": i_tomato, "tomatoReady": i_tomato,
    "cheese": i_cheese, "sauce": i_sauce, "dough": i_dough, "doughBaked": i_doughBaked,
    "coffee": i_coffee, "milk": i_milk, "drink": i_drink,
    "sausageRaw": i_sausageRaw, "sausageCooked": i_sausageCooked, "onion": i_onion,
    "tortilla": i_tortilla,
}

# ------------------------------------------------------------------ dish builders
def burger_layer(kind, z):
    if kind == "bun_bottom":
        d = sph("bb", 0.5, (0, 0, z - 0.04), M("bun_bot"), (1, 1, 0.32)); smooth(d, 1); return 0.13
    if kind == "bun_top":
        d = sph("bt", 0.5, (0, 0, z + 0.05), M("bun"), (1, 1, 0.48)); smooth(d, 1)
        import math as _m
        for i in range(6):
            a = i*(_m.tau/6); sph("s", 0.055, (0.22*_m.cos(a), 0.22*_m.sin(a), z+0.22), M("sesame"), (1,1,0.5))
        return 0.34
    if kind == "patty":
        d = cyl("p", 0.53, 0.51, 0.16, (0, 0, z + 0.08), M("patty")); smooth(d, 1, 0.02); return 0.16
    if kind == "cheese":
        c = box("c", (0.98, 0.98, 0.05), (0, 0, z + 0.03), M("cheese")); c.rotation_euler = (0, 0, math.radians(24)); return 0.08
    if kind == "lettuce":
        import math as _m
        for i in range(11):
            a=i*(_m.tau/11); sph("ll",0.16,(0.4*_m.cos(a),0.4*_m.sin(a),z+0.06),M("lettuce"),(1,1,0.55))
        return 0.12
    if kind == "tomato":
        cyl("tt", 0.5, 0.5, 0.09, (0, 0, z + 0.045), M("tomato")); return 0.1
    if kind == "sauce":
        cyl("ss", 0.46, 0.46, 0.05, (0, 0, z + 0.03), M("sauce")); return 0.06
    return 0.0

BURGER_PARTS = {  # recipe -> layer stack (bottom to top)
    "basic": ["bun_bottom", "patty", "bun_top"],
    "green": ["bun_bottom", "patty", "lettuce", "bun_top"],
    "fresh": ["bun_bottom", "patty", "lettuce", "tomato", "bun_top"],
    "cheese": ["bun_bottom", "patty", "cheese", "bun_top"],
    "deluxe": ["bun_bottom", "patty", "cheese", "lettuce", "tomato", "bun_top"],
    "saucy": ["bun_bottom", "patty", "cheese", "sauce", "bun_top"],
    "tower": ["bun_bottom", "patty", "cheese", "patty", "lettuce", "bun_top"],
    "bacon": ["bun_bottom", "patty", "sauce", "cheese", "bun_top"],
    "double": ["bun_bottom", "patty", "cheese", "patty", "bun_top"],
}
PIZZA_PARTS = {
    "margherita": ["cheese"], "garden": ["cheese", "lettuce"],
    "rosso": ["cheese", "tomato"], "supreme": ["cheese", "tomato", "lettuce"],
    "bianca": ["cheese"],
}
COFFEE = {
    "espresso": ("coffee", 0.44, False), "latte": ("coffee", 0.4, True),
    "sweet_latte": ("sauce", 0.4, True), "double_shot": ("coffee", 0.5, False),
    "cappuccino": ("coffee", 0.36, True),
}

def build_burger(rid):
    z = 0.02
    for layer in BURGER_PARTS[rid]:
        z += burger_layer(layer, z)

def build_pizza(rid):
    base = cyl("base", 0.62, 0.58, 0.13, (0, 0, 0.065), M("baked")); smooth(base, 1, 0.03)
    if rid != "bianca":
        cyl("psauce", 0.55, 0.55, 0.03, (0, 0, 0.13), M("sauce"))
    import math as _m
    if "cheese" in PIZZA_PARTS[rid]:
        ch = cyl("pch", 0.55, 0.55, 0.05, (0, 0, 0.155), M("pcheese")); smooth(ch, 1)
        for i in range(7):
            a = i*(_m.tau/7)+0.2; sph("mb", 0.06, (0.32*_m.cos(a), 0.32*_m.sin(a), 0.185), M("pcheese"), (1, 1, 0.5))
    for t in PIZZA_PARTS[rid]:
        if t == "tomato":
            for i in range(6):
                a=i*(_m.tau/6); cyl("pt",0.1,0.1,0.05,(0.34*_m.cos(a),0.34*_m.sin(a),0.19),M("tomato"))
        elif t == "lettuce":
            for i in range(7):
                a=i*(_m.tau/7)+0.3; sph("pl",0.08,(0.3*_m.cos(a),0.3*_m.sin(a),0.2),M("lettuce"),(1,1,0.6))
    if rid == "margherita":
        for i in range(3):
            a=i*(_m.tau/3)
            cyl("mt",0.09,0.09,0.05,(0.28*_m.cos(a),0.28*_m.sin(a),0.19),M("tomato"))
            sph("mbz",0.055,(0.28*_m.cos(a+0.6),0.28*_m.sin(a+0.6),0.2),M("lettuce"),(1,1,0.6))
    if rid == "bianca":
        for i in range(5):
            a=i*(_m.tau/5); sph("bz",0.06,(0.3*_m.cos(a),0.3*_m.sin(a),0.2),M("lettuce"),(1,1,0.6))

def build_coffee(rid):
    fill, fz, foam = COFFEE[rid]
    _cup(fill, fz, foam)

HOTDOG_PARTS = {
    "classic": ["sauce"], "cheesy": ["cheese", "sauce"], "loaded": ["cheese", "onion", "sauce"],
}

def build_hotdog(rid):
    for y in (-0.18, 0.18):                     # two bun rolls along X
        r = cyl("roll", 0.17, 0.17, 0.94, (0, y, 0.17), M("bun")); r.rotation_euler = (0, math.radians(90), 0); smooth(r, 1)
    s = cyl("saus", 0.15, 0.15, 0.82, (0, 0, 0.32), M("saus_ck")); s.rotation_euler = (0, math.radians(90), 0); smooth(s, 1)
    for e in (-0.38, 0.38): sph("sc", 0.15, (e, 0, 0.32), M("saus_ck"))
    parts = HOTDOG_PARTS[rid]
    if "cheese" in parts:
        box("ch", (0.72, 0.36, 0.03), (0, 0, 0.42), M("cheese"))
    if "onion" in parts:
        for x in (-0.24, -0.08, 0.08, 0.24):
            box("on", (0.08, 0.08, 0.06), (x, -0.06, 0.44), M("onion"))
    if "sauce" in parts:
        z = box("kz", (0.66, 0.06, 0.05), (0, 0.05, 0.45), M("sauce")); z.rotation_euler = (0, 0, math.radians(4))

TACO_PARTS = {
    "taco": ["beef", "lettuce", "cheese"],
    "carnita": ["beef", "cheese", "tomato", "onion"],
    "burrito": ["beef", "cheese", "sauce"],
    "quesadilla": ["cheese"],
    "nachos": ["cheese", "tomato"],
}

def _taco_fill(parts, cx=0.0, spread=0.3, z=0.26):
    import math as _m
    if "beef" in parts:
        sph("bf0", 0.2, (cx, 0, z), M("beef"), (1.5, 0.85, 0.8)); smooth(bpy.context.object, 1)
        for i in range(12):
            a = i * _m.tau / 12
            sph("bf", 0.085, (cx + spread * _m.cos(a) * 0.85, spread * 0.55 * _m.sin(a), z + 0.03), M("beef_hi"), (1, 1, 0.8))
    if "cheese" in parts:
        for i in range(9):
            a = i * _m.tau / 9 + 0.3
            box("cs", (0.14, 0.04, 0.03), (cx + spread * _m.cos(a) * 0.9, spread * 0.55 * _m.sin(a), z + 0.09), M("cheese_melt"))
    if "lettuce" in parts:
        for i in range(11):
            a = i * _m.tau / 11
            sph("ll", 0.08, (cx + spread * _m.cos(a) * 1.0, spread * 0.6 * _m.sin(a), z + 0.11), M("lettuce"), (1, 1, 0.6))
    if "tomato" in parts:
        for i in range(7):
            a = i * _m.tau / 7 + 0.5
            box("tm", (0.08, 0.08, 0.05), (cx + spread * _m.cos(a) * 0.8, spread * 0.5 * _m.sin(a), z + 0.1), M("tomato"))
    if "onion" in parts:
        for i in range(7):
            a = i * _m.tau / 7
            box("on", (0.07, 0.07, 0.04), (cx + spread * _m.cos(a) * 0.7, spread * 0.45 * _m.sin(a), z + 0.12), M("onion"))
    if "sauce" in parts:
        cyl("sz", 0.24, 0.24, 0.03, (cx, 0, z + 0.05), M("sauce"))

def build_taco(rid):
    # folded shell: two tortilla walls forming an open U, filling overflowing the crease
    for side in (-1, 1):
        w = cyl("shell", 0.58, 0.58, 0.06, (side * 0.22, 0, 0.34), M("tortilla")); smooth(w, 1, 0.02)
        w.rotation_euler = (math.radians(90), 0, side * math.radians(24))
        e = cyl("edge", 0.58, 0.58, 0.02, (side * 0.28, 0, 0.34), M("tort_edge"))
        e.rotation_euler = (math.radians(90), 0, side * math.radians(24))
    _taco_fill(TACO_PARTS[rid], 0.0, 0.3, 0.26)

def build_burrito(rid):
    body = cyl("wrap", 0.3, 0.3, 1.0, (0, 0, 0.32), M("tortilla")); body.rotation_euler = (0, math.radians(90), 0); smooth(body, 1)
    fold = cyl("fold", 0.31, 0.31, 0.3, (0.02, 0, 0.32), M("tort_edge")); fold.rotation_euler = (0, math.radians(90), 0); smooth(fold, 1)
    for e, sgn in ((-0.48, -1), (0.48, 1)):
        c = cyl("end", 0.3, 0.13, 0.14, (e, 0, 0.32), M("tort_edge")); c.rotation_euler = (0, math.radians(90) * sgn, 0); smooth(c, 1)
    for gx in (-0.28, 0.0, 0.28):                                          # toasted grill stripes on top
        box("gm", (0.06, 0.42, 0.02), (gx, 0, 0.61), M("tort_char"))
    for (dx, dy) in [(-0.34, 0.08), (0.12, -0.1), (0.36, 0.12), (-0.05, 0.14)]:
        sph("sp", 0.045, (dx, dy, 0.6), M("tort_char"), (1, 1, 0.3))

def build_quesadilla(rid):
    d = cyl("q", 0.64, 0.64, 0.1, (0, 0, 0.05), M("tortilla")); smooth(d, 1, 0.03)
    cyl("rim", 0.64, 0.64, 0.04, (0, 0, 0.02), M("tort_edge"))             # toasted rim underneath
    import math as _m
    box("fold", (0.05, 1.24, 0.12), (0, 0, 0.07), M("tort_edge"))          # centre fold line
    for gx in (-0.36, -0.12, 0.12, 0.36):                                  # bold grill marks
        box("gm", (0.07, 0.86, 0.025), (gx, 0, 0.105), M("tort_char"))
    for gy in (-0.28, 0.28):
        box("gm2", (0.9, 0.06, 0.02), (0, gy, 0.104), M("tort_char"))
    for i in range(7):                                                     # melted cheese oozing at the rim
        a = i * _m.tau / 7 + 0.4
        sph("oz", 0.09, (0.56 * _m.cos(a), 0.56 * _m.sin(a), 0.1), M("cheese_melt"), (1, 1, 0.45))

def build_nachos(rid):
    import math as _m
    parts = TACO_PARTS[rid]
    cyl("plate", 0.5, 0.46, 0.06, (0, 0, 0.03), M("dish")); smooth(bpy.context.object, 1)
    for i in range(12):                                                    # overlapping chip mound
        a = i * _m.tau / 12 + (i % 2) * 0.26
        r = 0.1 + (i % 3) * 0.13
        z = 0.09 + (i % 3) * 0.05
        ch = cyl("chip", 0.19, 0.02, 0.05, (r * _m.cos(a), r * _m.sin(a), z), M("tortilla"), verts=3)
        ch.rotation_euler = (math.radians(18 + (i % 3) * 10), 0, a + 0.4)
    cz = sph("cz", 0.3, (0, 0, 0.2), M("cheese_melt"), (1.3, 1.3, 0.35)); smooth(cz, 1)   # cheese blanket
    if "tomato" in parts:
        for i in range(7):
            a = i * _m.tau / 7
            box("tm", (0.06, 0.06, 0.05), (0.22 * _m.cos(a), 0.22 * _m.sin(a), 0.27), M("tomato"))
    for i in range(5):
        a = i * _m.tau / 5 + 0.6
        box("jp", (0.06, 0.06, 0.03), (0.14 * _m.cos(a), 0.14 * _m.sin(a), 0.29), M("jalapeno"))

def build_taco_dish(rid):
    if rid == "burrito": build_burrito(rid)
    elif rid == "quesadilla": build_quesadilla(rid)
    elif rid == "nachos": build_nachos(rid)
    else: build_taco(rid)

DISHES = {}
for k in TACO_PARTS: DISHES[k] = ("taco", k)
for k in BURGER_PARTS: DISHES[k] = ("burger", k)
for k in PIZZA_PARTS: DISHES[k] = ("pizza", k)
for k in COFFEE: DISHES[k] = ("coffee", k)
for k in HOTDOG_PARTS: DISHES[k] = ("hotdog", k)

def build_dish(rid):
    kind, _ = DISHES[rid]
    if kind == "burger": build_burger(rid)
    elif kind == "pizza": build_pizza(rid)
    elif kind == "hotdog": build_hotdog(rid)
    elif kind == "taco": build_taco_dish(rid)
    else: build_coffee(rid)

# ------------------------------------------------------------------ scene / render
RES = args.res
def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.film_transparent = True
    sc.render.resolution_x = RES; sc.render.resolution_y = RES
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    try: sc.eevee.taa_render_samples = 48
    except Exception: pass
    tgt = bpy.data.objects.new("t", None); sc.collection.objects.link(tgt); tgt.location = (0, 0, 0.2)
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 1.5
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam)
    elev = math.radians(52); D = 10
    cam.location = (0, D*math.cos(elev), 0.2 + D*math.sin(elev))
    c = cam.constraints.new('TRACK_TO'); c.target = tgt; c.track_axis = 'TRACK_NEGATIVE_Z'; c.up_axis = 'UP_Y'
    sc.camera = cam
    def sun(rot, e, col=(1, 1, 1)):
        d = bpy.data.lights.new("s", 'SUN'); d.energy = e; d.color = col
        try: d.angle = math.radians(18)
        except Exception: pass
        o = bpy.data.objects.new("s", d); sc.collection.objects.link(o); o.rotation_euler = [math.radians(a) for a in rot]
    sun((52, 8, 28), 4.4, (1, 0.97, 0.9)); sun((60, 0, -150), 1.6, (0.85, 0.9, 1)); sun((-26, 0, 178), 2.4, (1, 0.96, 0.88))
    w = bpy.data.worlds.new("W"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.46, 0.48, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.75
    sc.world = w
    try: sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
    except Exception: pass

def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

def render_item(name, builder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    builder()
    setup_scene()
    render_to(os.path.join(args.out, name + ".png"))

def main():
    os.makedirs(args.out, exist_ok=True)
    if args.item:
        if args.item in INGREDIENTS: render_item(args.item, INGREDIENTS[args.item])
        elif args.item in DISHES: render_item(args.item, lambda: build_dish(args.item))
    elif args.mode == "ingredients":
        for n, b in INGREDIENTS.items(): render_item(n, b)
    elif args.mode == "dishes":
        for n in DISHES: render_item(n, (lambda nn: (lambda: build_dish(nn)))(n))
    print("FOODGEN_DONE", args.mode, args.item)

main()
