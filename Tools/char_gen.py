"""
Rushhouse character generator (Blender 5.1, headless).

Builds a stylized PlateUp/Overcooked-flavoured character from primitives using a
"rigid-hierarchy paperdoll" rig (each limb segment pivots about its own joint;
no skinning). Renders high-angle top-down orthographic sprite frames with alpha
into the game's CharactersAnim naming scheme.

Run:
  blender -b -P char_gen.py -- --char cook --mode preview --out <dir>
  blender -b -P char_gen.py -- --char cook --mode full    --out <dir>
"""
import bpy, math, sys, os, argparse
from mathutils import Vector, Euler

argv = sys.argv
argv = argv[argv.index("--") + 1:] if "--" in argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--char", default="cook")
ap.add_argument("--mode", default="preview")   # preview | full
ap.add_argument("--out", required=True)
ap.add_argument("--res", type=int, default=384)
args = ap.parse_args(argv)

RES_W = args.res
RES_H = int(round(args.res * 4 / 3 / 2) * 2)   # portrait 3:4, even

SKIN = (0.93, 0.76, 0.60)
DARK = (0.10, 0.11, 0.13)

CHARS = {
    "cook": dict(jacket=(0.94,0.94,0.95), trousers=(0.16,0.17,0.20), hair=(0.09,0.08,0.09),
        band=(0.96,0.96,0.97), buttons=(0.12,0.12,0.14), shoes=(0.08,0.08,0.09), prop="pan"),
    "player": dict(jacket=(0.93,0.94,0.95), trousers=(0.16,0.17,0.20), hair=(0.09,0.08,0.09),
        band=(0.90,0.55,0.20), buttons=(0.12,0.12,0.14), shoes=(0.08,0.08,0.09), prop="pan"),
    "waiter": dict(jacket=(0.13,0.14,0.17), trousers=(0.10,0.10,0.12), hair=(0.20,0.13,0.07),
        band=None, buttons=(0.85,0.85,0.88), shoes=(0.06,0.06,0.07), vest=True, prop="plate"),
    "prepper": dict(jacket=(0.20,0.55,0.52), trousers=(0.15,0.16,0.19), hair=(0.30,0.18,0.09),
        band=None, buttons=(0.10,0.10,0.12), shoes=(0.08,0.08,0.09), prop="board"),
    "customerHappy": dict(jacket=(0.30,0.52,0.78), trousers=(0.22,0.24,0.30), hair=(0.28,0.16,0.08),
        band=None, buttons=(0.20,0.34,0.55), shoes=(0.30,0.22,0.14), civilian=True, prop=None),
    "customerNeutral": dict(jacket=(0.78,0.42,0.30), trousers=(0.22,0.24,0.30), hair=(0.10,0.09,0.09),
        band=None, buttons=(0.55,0.28,0.20), shoes=(0.22,0.16,0.12), civilian=True, prop=None),
    "customerAngry": dict(jacket=(0.60,0.22,0.22), trousers=(0.16,0.16,0.18), hair=(0.12,0.09,0.07),
        band=None, buttons=(0.40,0.14,0.14), shoes=(0.10,0.09,0.09), civilian=True, prop=None),
    "customerWalk": dict(jacket=(0.42,0.55,0.32), trousers=(0.24,0.22,0.28), hair=(0.22,0.14,0.08),
        band=None, buttons=(0.30,0.40,0.22), shoes=(0.28,0.20,0.13), civilian=True, prop=None),
}

# ---------------------------------------------------------------- helpers
def mat(name, rgb, rough=0.62, metal=0.0):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m

def apply_mat(o, m):
    o.data.materials.clear(); o.data.materials.append(m)

def smooth(o, subsurf=1, bevel=0.0):
    for p in o.data.polygons: p.use_smooth = True
    if bevel > 0:
        bm = o.modifiers.new("bev",'BEVEL'); bm.width=bevel; bm.segments=2
    if subsurf > 0:
        s = o.modifiers.new("sub",'SUBSURF'); s.levels=subsurf; s.render_levels=subsurf

def cyl(name, r1, r2, depth, loc, m, verts=20):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
    o=bpy.context.object; o.name=name; apply_mat(o,m); return o

def sph(name, r, loc, m, scale=(1,1,1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=22, ring_count=14)
    o=bpy.context.object; o.name=name; o.scale=scale; apply_mat(o,m); return o

def box(name, size, loc, m):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o=bpy.context.object; o.name=name; o.scale=(size[0]/2,size[1]/2,size[2]/2); apply_mat(o,m); return o

def set_pivot(o, point):
    bpy.context.scene.cursor.location = Vector(point)
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

def parent_to(child, parent):
    bpy.ops.object.select_all(action='DESELECT')
    child.select_set(True); parent.select_set(True)
    bpy.context.view_layer.objects.active=parent
    bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)

def join(objs, name):
    objs=[o for o in objs if o]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.object.join()
    o=bpy.context.object; o.name=name; return o

def deg(o, x=0,y=0,z=0):
    o.rotation_euler = Euler((math.radians(x),math.radians(y),math.radians(z)),'XYZ')

# ---------------------------------------------------------------- build
def build_character(cfg):
    m_skin=mat("skin",SKIN,0.55)
    m_j=mat("j"+str(cfg['jacket']),cfg['jacket'],0.68)
    m_t=mat("t"+str(cfg['trousers']),cfg['trousers'],0.7)
    m_h=mat("h"+str(cfg['hair']),cfg['hair'],0.8)
    m_b=mat("b"+str(cfg['buttons']),cfg['buttons'],0.4)
    m_s=mat("s"+str(cfg['shoes']),cfg['shoes'],0.5)
    m_dark=mat("dk",DARK,0.45)
    m_eye=mat("eye",(0.05,0.05,0.06),0.3)
    civ=cfg.get('civilian',False)
    P={}

    pelvis=cyl("pelvis",0.33,0.30,0.20,(0,0,0.70),m_t); smooth(pelvis,1,0.03)
    P['hips']=pelvis

    T=[]
    jk=cyl("jk",0.31,0.345,0.50,(0,0,0.98),m_j,24); smooth(jk,1,0.03); T.append(jk)
    sh=sph("sh",0.345,(0,0,1.16),m_j,(1.0,0.85,0.6)); smooth(sh,1); T.append(sh)
    if civ:
        cl=sph("cl",0.12,(0,0.24,1.12),m_skin,(1.4,0.5,0.7)); smooth(cl,1); T.append(cl)
    else:
        for zz in (1.12,1.0,0.88,0.76):
            for xx in (-0.09,0.09):
                T.append(sph("bt",0.028,(xx,0.30,zz),m_b))
        lp=box("lp",(0.24,0.06,0.10),(0,0.29,1.16),m_j); smooth(lp,0,0.02); T.append(lp)
        if cfg.get('vest'):
            vs=cyl("vs",0.315,0.35,0.48,(0,0.02,0.98),m_dark,24); smooth(vs,1,0.03); T.append(vs)
    P['torso']=join(T,"torso")

    H=[]
    hd=sph("hd",0.30,(0,0,1.46),m_skin,(1.0,0.98,1.02)); smooth(hd,1); H.append(hd)
    for xx in (-0.29,0.29): H.append(sph("ear",0.06,(xx,0,1.44),m_skin,(0.6,0.8,1.0)))
    H.append(sph("nose",0.045,(0,0.29,1.42),m_skin,(1,1.2,1)))
    for xx in (-0.115,0.115):
        H.append(sph("eye",0.05,(xx,0.255,1.47),m_eye,(0.9,0.7,1.15)))
        H.append(box("brow",(0.10,0.022,0.028),(xx,0.255,1.565),m_h))
    H.append(box("mouth",(0.11,0.02,0.03),(0,0.275,1.35),mat("mouth",(0.45,0.24,0.22))))
    # hair: flat crown cap high + back clump, forehead & face left clear
    crown=sph("hair",0.30,(0,-0.12,1.70),m_h,(1.05,1.0,0.52)); smooth(crown,1); H.append(crown)
    back=sph("hairb",0.25,(0,-0.20,1.50),m_h,(1.1,0.92,1.0)); smooth(back,1); H.append(back)
    for (hx,hy) in [(-0.15,-0.02),(0.0,-0.05),(0.15,-0.01),(-0.08,-0.14),(0.08,-0.14)]:
        sp=cyl("tuft",0.07,0.005,0.18,(hx,hy,1.83),m_h,8); sp.rotation_euler=(math.radians(hx*44),0,0); H.append(sp)
    if cfg.get('band'):
        m_bd=mat("bd"+str(cfg['band']),cfg['band'],0.6)
        bpy.ops.mesh.primitive_torus_add(location=(0,-0.01,1.55),major_radius=0.30,minor_radius=0.045)
        bd=bpy.context.object; bd.name="bd"; bd.scale=(1.02,1.05,1.0); apply_mat(bd,m_bd)
        for p in bd.data.polygons: p.use_smooth=True
        H.append(bd)
        H.append(sph("knot",0.06,(0,-0.33,1.53),m_bd,(1,1,0.8)))
        H.append(box("tail",(0.03,0.04,0.16),(0.05,-0.33,1.44),m_bd))
    P['head']=join(H,"head")

    def arm(side):
        sx=0.34*side
        U=[]
        u=cyl("ua",0.095,0.085,0.34,(sx,0,0.98),m_j,14); smooth(u,1); U.append(u)
        U.append(sph("us",0.11,(sx,0,1.14),m_j)); u2=join(U,"ua%d"%side)
        F=[]
        f=cyl("fa",0.082,0.075,0.30,(sx,0,0.66),m_skin,14); smooth(f,1); F.append(f)
        c=cyl("cf",0.10,0.095,0.06,(sx,0,0.80),m_j,14); smooth(c,1); F.append(c)
        F.append(sph("hand",0.10,(sx,0,0.50),m_skin,(1,0.85,1.1))); f2=join(F,"fa%d"%side)
        return u2,f2
    P['armR'],P['foreR']=arm(+1)
    P['armL'],P['foreL']=arm(-1)

    def leg(side):
        sx=0.15*side
        t=cyl("th",0.13,0.12,0.30,(sx,0,0.56),m_t,14); smooth(t,1)
        S=[]
        s=cyl("sn",0.115,0.10,0.30,(sx,0,0.26),m_t,14); smooth(s,1); S.append(s)
        S.append(sph("shoe",0.12,(sx,0.05,0.09),m_s,(1.0,1.7,0.75))); s2=join(S,"sn%d"%side)
        return t,s2
    P['legR'],P['shinR']=leg(+1)
    P['legL'],P['shinL']=leg(-1)

    P['prop']=None
    pr=cfg.get('prop')
    if pr=="pan":
        m_pan=mat("pan",(0.13,0.13,0.15),0.35,0.6); m_ph=mat("panh",(0.20,0.45,0.85),0.4)
        bowl=cyl("pb",0.20,0.20,0.05,(0,0,0),m_pan,24); smooth(bowl,1)
        inr=cyl("pi",0.165,0.165,0.03,(0,0,0.03),mat("pin",(0.17,0.17,0.19)),24); smooth(inr,0)
        hdl=box("phd",(0.055,0.34,0.05),(0,-0.32,0.02),m_ph); smooth(hdl,0,0.02)
        P['prop']=join([bowl,inr,hdl],"prop")
    elif pr=="plate":
        m_pl=mat("plw",(0.95,0.95,0.96),0.3)
        d=cyl("pl",0.19,0.19,0.03,(0,0,0),m_pl,24); smooth(d,1)
        fo=sph("fo",0.09,(0,0,0.05),mat("fd",(0.85,0.55,0.25)),(1,1,0.6)); smooth(fo,1)
        P['prop']=join([d,fo],"prop")
    elif pr=="board":
        d=box("bd",(0.34,0.24,0.04),(0,0,0),mat("board",(0.55,0.36,0.18),0.7)); smooth(d,0,0.02)
        for i,(fx,fc) in enumerate([(-0.09,(0.85,0.2,0.2)),(0.0,(0.9,0.8,0.3)),(0.09,(0.3,0.7,0.3))]):
            v=sph("vg",0.05,(fx,0,0.05),mat("vg%d"%i,fc)); smooth(v,1); d=join([d,v],"bd")
        P['prop']=d
    return P

# ---------------------------------------------------------------- rig
def rig(P):
    root=bpy.data.objects.new("root",None); bpy.context.scene.collection.objects.link(root)
    set_pivot(P['hips'],(0,0,0.72))
    set_pivot(P['torso'],(0,0,0.78))
    set_pivot(P['head'],(0,0,1.16))
    for sd,sx in (('R',0.34),('L',-0.34)):
        set_pivot(P['arm'+sd],(sx,0,1.12)); set_pivot(P['fore'+sd],(sx,0,0.80))
    for sd,sx in (('R',0.15),('L',-0.15)):
        set_pivot(P['leg'+sd],(sx,0,0.70)); set_pivot(P['shin'+sd],(sx,0,0.42))
    if P.get('prop'):
        # place pan/plate into the right hand, then pivot at grip
        P['prop'].location=(0.34,0.34,0.52)
        set_pivot(P['prop'],(0.34,0.10,0.52))
    parent_to(P['torso'],P['hips']); parent_to(P['head'],P['torso'])
    for sd in ('R','L'):
        parent_to(P['arm'+sd],P['torso']); parent_to(P['fore'+sd],P['arm'+sd])
        parent_to(P['leg'+sd],P['hips']); parent_to(P['shin'+sd],P['leg'+sd])
    if P.get('prop'): parent_to(P['prop'],P['foreR'])
    parent_to(P['hips'],root)
    P['root']=root
    P['_base_hips']=P['hips'].location.copy()
    return P

ROT=['hips','torso','head','armR','armL','foreR','foreL','legR','legL','shinR','shinL']

def pose(P, state, f, holds):
    for k in ROT: deg(P[k])
    P['hips'].location=P['_base_hips'].copy()
    TAU=math.tau
    carrying = state in ('carry','carrywalk')
    if P.get('prop'): P['prop'].hide_render = carrying   # game draws the carried item instead
    # baseline arm posture
    if carrying:
        deg(P['armR'],x=60,z=15); deg(P['foreR'],x=-52)
        deg(P['armL'],x=60,z=-15); deg(P['foreL'],x=-52)
    elif holds:
        deg(P['armR'],x=64,z=6); deg(P['foreR'],x=-34)
        deg(P['armL'],x=16,y=6); deg(P['foreL'],x=-14)
    else:
        deg(P['armR'],x=9,y=-5); deg(P['armL'],x=9,y=5)
        deg(P['foreR'],x=-7); deg(P['foreL'],x=-7)
    # slight knee softness
    deg(P['legR'],x=-3); deg(P['legL'],x=-3)

    if state=='idle':
        up=(0.5-0.5*math.cos(f/2*TAU))*0.025
        P['hips'].location.z+=up
        deg(P['head'],x=2*math.sin(f/2*TAU))
        if holds:
            deg(P['armR'],x=64+2*math.sin(f/2*TAU),z=6); deg(P['foreR'],x=-34-3*up*40)
        else:
            deg(P['armR'],x=9+3*math.sin(f/2*TAU),y=-5); deg(P['armL'],x=9-3*math.sin(f/2*TAU),y=5)

    elif state=='walk' or state=='carrywalk':
        ph=f/4*TAU
        A=27
        deg(P['legR'],x=-3+A*math.sin(ph)); deg(P['legL'],x=-3+A*math.sin(ph+math.pi))
        deg(P['shinR'],x=-max(0.0,-math.sin(ph))*34); deg(P['shinL'],x=-max(0.0,-math.sin(ph+math.pi))*34)
        P['hips'].location.z+=abs(math.sin(ph))*0.03
        deg(P['torso'],z=5*math.sin(ph))
        deg(P['head'],z=-3*math.sin(ph))
        if carrying:
            deg(P['armR'],x=60,z=15); deg(P['foreR'],x=-52)
            deg(P['armL'],x=60,z=-15); deg(P['foreL'],x=-52)
        elif holds:
            deg(P['armR'],x=64,z=6); deg(P['foreR'],x=-34+4*math.sin(ph))
            deg(P['armL'],x=16+26*math.sin(ph)); deg(P['foreL'],x=-14)
        else:
            deg(P['armR'],x=9+26*math.sin(ph+math.pi),y=-5)
            deg(P['armL'],x=9+26*math.sin(ph),y=5)

    elif state=='carry':
        up=(0.5-0.5*math.cos(f/2*TAU))*0.02
        P['hips'].location.z+=up
        deg(P['head'],x=3+2*math.sin(f/2*TAU))
        deg(P['armR'],x=60,z=15); deg(P['foreR'],x=-52-2*math.sin(f/2*TAU))
        deg(P['armL'],x=60,z=-15); deg(P['foreL'],x=-52-2*math.sin(f/2*TAU))

    elif state=='sit':
        P['hips'].location.z-=0.20
        deg(P['legR'],x=82,z=6); deg(P['legL'],x=82,z=-6)
        deg(P['shinR'],x=-84); deg(P['shinL'],x=-84)
        deg(P['torso'],x=-4)
        bob=math.sin(f/2*TAU)
        deg(P['head'],x=5+2*bob)
        if holds:
            deg(P['armR'],x=54,z=6); deg(P['foreR'],x=-44)
            deg(P['armL'],x=54,z=-6); deg(P['foreL'],x=-44)
        else:
            deg(P['armR'],x=36+2*bob,y=-6); deg(P['foreR'],x=-46)
            deg(P['armL'],x=36-2*bob,y=6); deg(P['foreL'],x=-46)

    elif state=='act':
        if holds:
            sh=[-20,10,-8][f]
            deg(P['armR'],x=64+[0,-7,0][f],z=6); deg(P['foreR'],x=-34+sh)
            deg(P['armL'],x=16,y=6); deg(P['foreL'],x=-14)
            deg(P['torso'],x=5); deg(P['head'],x=8)
        else:
            ch=[-26,2,-26][f]
            deg(P['armR'],x=34,y=-5); deg(P['foreR'],x=-20+ch)
            deg(P['armL'],x=34,y=5); deg(P['foreL'],x=-20+ch)
            deg(P['torso'],x=10); deg(P['head'],x=10)

WORKER_STATES=[('idle',2),('walk',4),('act',3),('carry',2),('carrywalk',4)]
CUST_STATES=[('idle',2),('walk',4),('act',3),('sit',2)]
def states_for(char):
    return CUST_STATES if char.startswith('customer') else WORKER_STATES
DIRS={'front':0.0,'back':180.0,'right':-90.0,'left':90.0}

# ---------------------------------------------------------------- scene / render
def setup_scene():
    sc=bpy.context.scene
    sc.render.engine='BLENDER_EEVEE'
    sc.render.film_transparent=True
    sc.render.resolution_x=RES_W; sc.render.resolution_y=RES_H
    sc.render.image_settings.file_format='PNG'; sc.render.image_settings.color_mode='RGBA'
    try: sc.eevee.taa_render_samples=48
    except Exception: pass
    for opt in ('use_gtao','use_raytracing','use_shadows'):
        try: setattr(sc.eevee,opt,True)
        except Exception: pass
    # camera: high-angle orthographic, looking at chest height
    tgt=bpy.data.objects.new("tgt",None); sc.collection.objects.link(tgt); tgt.location=(0,0,0.98)
    cam_d=bpy.data.cameras.new("Cam"); cam_d.type='ORTHO'; cam_d.ortho_scale=2.62
    cam=bpy.data.objects.new("Cam",cam_d); sc.collection.objects.link(cam)
    elev=math.radians(47); D=12
    cam.location=(0, D*math.cos(elev), 0.86+D*math.sin(elev))
    c=cam.constraints.new('TRACK_TO'); c.target=tgt; c.track_axis='TRACK_NEGATIVE_Z'; c.up_axis='UP_Y'
    sc.camera=cam
    # lights
    def sun(name,rot,energy,color=(1,1,1)):
        d=bpy.data.lights.new(name,'SUN'); d.energy=energy; d.color=color
        try: d.angle=math.radians(15)
        except Exception: pass
        o=bpy.data.objects.new(name,d); sc.collection.objects.link(o); o.rotation_euler=[math.radians(a) for a in rot]; return o
    sun("key",(50,10,30),4.6,(1.0,0.96,0.88))     # warm key, brighter
    sun("fill",(62,0,-150),1.5,(0.82,0.88,1.0))    # cool fill
    sun("rim",(-24,0,178),3.2,(1.0,0.97,0.9))      # strong rim for edge pop
    sun("top",(0,0,0),1.1,(1.0,1.0,1.0))           # soft top
    # world ambient (brighter, faintly warm so characters read on dark floor)
    w=bpy.data.worlds.new("W"); w.use_nodes=True
    w.node_tree.nodes["Background"].inputs[0].default_value=(0.42,0.43,0.46,1)
    w.node_tree.nodes["Background"].inputs[1].default_value=0.7
    sc.world=w
    try:
        vt=sc.view_settings; vt.view_transform='Standard'; vt.look='None'
    except Exception: pass

def render_to(path):
    bpy.context.scene.render.filepath=path
    bpy.ops.render.render(write_still=True)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)   # clear default cube/cam/light
    cfg=CHARS[args.char]; holds=cfg.get('prop') is not None
    P=build_character(cfg); rig(P); setup_scene()
    os.makedirs(args.out, exist_ok=True)
    if args.mode=='preview':
        for dname,dz in DIRS.items():
            deg(P['root'],z=dz); pose(P,'idle',0,holds)
            render_to(os.path.join(args.out, f"prev_{args.char}_{dname}.png"))
        # one frame per extra state, front
        deg(P['root'],z=0); pose(P,'walk',1,holds); render_to(os.path.join(args.out,f"prev_{args.char}_walk1.png"))
        pose(P,'act',0,holds); render_to(os.path.join(args.out,f"prev_{args.char}_act0.png"))
        pose(P,'carry',0,holds); render_to(os.path.join(args.out,f"prev_{args.char}_carry0.png"))
        pose(P,'carrywalk',1,holds); render_to(os.path.join(args.out,f"prev_{args.char}_carrywalk1.png"))
        pose(P,'sit',0,holds); render_to(os.path.join(args.out,f"prev_{args.char}_sit0.png"))
    else:
        for dname,dz in DIRS.items():
            deg(P['root'],z=dz)
            for st,n in states_for(args.char):
                for f in range(n):
                    pose(P,st,f,holds)
                    render_to(os.path.join(args.out, f"{args.char}_{dname}_{st}_{f}.png"))
    print("CHARGEN_DONE", args.char, args.mode)

main()
