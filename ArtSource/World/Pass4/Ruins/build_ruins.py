"""Run in Blender background with --disable-autoexec --python build_ruins.py.
Loads original world materials/library without executing its generation scripts.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
OUT=ROOT/'Assets/Realm/Art/World/Pass4/Ruins'
bpy.ops.wm.open_mainfile(filepath=str(HERE.parents[1]/'RealmWorld.blend'))
exec(compile(bpy.data.texts['world_lib.py'].as_string(),'world_lib.py','exec'))
random.seed(404)
for o in list(bpy.data.objects): bpy.data.objects.remove(o,do_unlink=True)
col=collection('Pass4_Ruins')
assets=[]

def stone(k,c,s,mat='W_Rubble',tilt=0.018):
    k.box(c,s,mat,rot=(random.uniform(-tilt,tilt),random.uniform(-tilt,tilt),random.uniform(-tilt,tilt)),taper=random.uniform(0.015,0.055))

def finish(k):
    o=k.finish(col)
    bpy.context.view_layer.objects.active=o
    o.select_set(True)
    mod=o.modifiers.new('Soft chipped stone edges','BEVEL'); mod.width=0.038; mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Flat faces retain the existing kit's faceted stone language.
    assets.append(o); o.select_set(False)
    return o

for name,heights in [('RuinWall_A',[7,7,6,5,4,4]),('RuinWall_B',[4,5,6,5,3,2])]:
    k=Kit(name)
    for row in range(max(heights)):
        # staggered joints while retaining flush placement edges
        breaks=[-2.5]+[x for x in [-2.5+(.42 if row%2 else .83)+i*.83 for i in range(7)] if -2.49<x<2.49]+[2.5]
        for a,b in zip(breaks,breaks[1:]):
            x=(a+b)/2; idx=min(5,int((x+2.5)/5*6))
            if row>=heights[idx]: continue
            stone(k,(x,random.uniform(-.035,.035),row*.55+.272),(b-a-.025,.8+random.uniform(-.07,.07),.525))
    # Deep surviving buttress at the unbroken end.
    for row in range(5): stone(k,(-1.95,.43,row*.55+.272),(.65,.65,.525),'W_Rock')
    finish(k)

k=Kit('RuinArch')
for side in (-1,1):
    for row in range(5):
        stone(k,(side*2.38,0,row*.52+.26),(1.15,1.1,.50))
    stone(k,(side*2.38,0,2.62),(1.28,1.22,.19),'W_Rock')
# Radial voussoirs: open 3.6m passage, no hidden wall inside arch.
for i in range(13):
    a=i*math.pi/13+.009; b=(i+1)*math.pi/13-.009
    inner=1.8; outer=2.65
    poly=[(inner*math.cos(a),2.65+inner*math.sin(a)),(outer*math.cos(a),2.65+outer*math.sin(a)),(outer*math.cos(b),2.65+outer*math.sin(b)),(inner*math.cos(b),2.65+inner*math.sin(b))]
    k.prism(poly,-.55,.55,'W_Rock' if i==6 else 'W_Rubble')
finish(k)

k=Kit('RuinPillar')
stone(k,(0,0,.16),(1.65,1.65,.32),'W_Rock',0)
stone(k,(0,0,.41),(1.4,1.4,.18),'W_Rock',0)
for row in range(6):
    z=.5+row*.49
    for x in (-.3,.3): stone(k,(x,0,z+.235),(.58,1.14,.47))
stone(k,(0,0,3.49),(1.4,1.4,.18),'W_Rock')
k.prism([(-.56,3.57),(.56,3.57),(.55,3.87),(.1,4.08),(-.18,3.81),(-.55,3.98)],-.56,.56,'W_Rubble')
finish(k)

k=Kit('RuinStairs')
for step in range(7):
    h=(step+1)*.25; y=-1.55+step*.47
    for x in (-1.,0.,1.): stone(k,(x,y,h/2),(.978,.46,h),'W_Rock',0)
# low irregular side remnants frame the climb without obstructing treads
for side in (-1,1):
    for step in range(2,7):
        stone(k,(side*1.52,-1.55+step*.47,(step+1)*.25+.18),(.25,.44,.36))
finish(k)

k=Kit('RuinRubble')
for i in range(23):
    a=random.uniform(0,math.tau); r=random.uniform(0,1.03)
    sx=random.uniform(.3,.65); sy=random.uniform(.3,.58); sz=random.uniform(.2,.44)
    z=sz/2+(max(0,.65-r)*.65 if i>8 else 0)
    stone(k,(math.cos(a)*r,math.sin(a)*r,z),(sx,sy,sz),'W_Rubble' if i%3 else 'W_Rock',.23)
rubble=finish(k)
# Spread the rubble footprint without stretching its vertical silhouette.
for v in rubble.data.vertices:
    v.co.x*=1.65; v.co.y*=1.35

OUT.mkdir(parents=True,exist_ok=True)
report=[]
for o in assets:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    # Seat each mesh precisely on z=0, keeping origin at ground center.
    low=min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices: v.co.z-=low
    o.data.calc_loop_triangles()
    bpy.context.view_layer.update()
    report.append({'name':o.name,'dimensions_xyz_m':[round(x,3) for x in o.dimensions],'triangles':len(o.data.loop_triangles),'materials':[m.name for m in o.data.materials]})
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,use_mesh_modifiers=True,path_mode='STRIP',bake_anim=False)
(HERE/'asset_report.json').write_text(json.dumps(report,indent=2))
# Resolve original image paths before saving beside the new blend.
for im in bpy.data.images:
    if im.filepath: im.filepath=bpy.path.abspath(im.filepath)
for t in list(bpy.data.texts): bpy.data.texts.remove(t)
bpy.data.texts.new('build_ruins.py').write(Path(__file__).read_text())
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Ruins.blend'))
# Separate contact-sheet staging; never export these placement offsets.
for o,pos in zip(assets,[(-7,3,0),(0,3,0),(7,3,0),(-7,-4,0),(0,-4,0),(7,-4,0)]): o.location=pos
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.render.resolution_x=1800; scene.render.resolution_y=1050; scene.render.resolution_percentage=100
scene.world.color=(.18,.18,.18)
bpy.ops.object.camera_add(location=(19,-28,24)); cam=bpy.context.object
cam.rotation_euler=(Vector((0,0,1.2))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=28; scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(0,-5,16)); bpy.context.object.data.energy=2700; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=15
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.03)); ground=bpy.context.object
m=bpy.data.materials.new('PreviewGround'); m.diffuse_color=(.075,.09,.10,1); ground.data.materials.append(m)
for o in assets:
    bpy.ops.object.text_add(location=o.location+Vector((-2,-1.7,.03)))
    label=bpy.context.object; label.data.body=o.name; label.data.size=.38
scene.render.filepath=str(HERE/'Ruins_ContactSheet.png'); bpy.ops.render.render(write_still=True)
print('RUINS_REPORT',json.dumps(report))
