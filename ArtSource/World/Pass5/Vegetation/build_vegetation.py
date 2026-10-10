"""Blender --background --disable-autoexec --python build_vegetation.py"""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[3]
OUT=PROJECT/'Assets/Realm/Art/World/Pass5/Vegetation'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(HERE.parents[1]/'RealmWorld.blend'))
exec(bpy.data.texts['world_lib.py'].as_string(),globals())
tr={}; exec(bpy.data.texts['tree.py'].as_string(),tr)
card=tr['card']
for o in list(bpy.data.objects): bpy.data.objects.remove(o,do_unlink=True)
col=collection('Pass5_Vegetation'); objects=[]
random.seed(5501)
# Existing atlas: grass bottom-left, fern bottom-right, broadleaf top-left.
# Preserve atlas UVs; explicitly wire alpha for the saved Blender preview.
fol=bpy.data.materials['T_Foliage']; nt=fol.node_tree
bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
im=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image and 'BaseColor' in n.image.name)
nt.links.new(im.outputs['Alpha'],bs.inputs['Alpha']); fol.use_backface_culling=False
def branch(k,a,b,r,r2):
    a=Vector(a); b=Vector(b); k.cylinder(a,r,(b-a).length,'W_Bark',seg=6,axis=b-a,r2=r2)
for variant in range(2):
    k=Kit('GrassTuft_'+('A' if variant==0 else 'B')); rng=random.Random(51+variant)
    for i in range(24 if variant==0 else 32):
        a=rng.uniform(0,math.tau); r=rng.uniform(.03,.32 if variant==0 else .43)
        card(k,(math.cos(a)*r,math.sin(a)*r,0),rng.uniform(.15,.28),rng.uniform(.25,.43), 'grass',yaw=rng.uniform(0,math.tau),tilt=rng.uniform(-.15,.25))
    objects.append(k.finish(col))
k=Kit('FernPatch'); rng=random.Random(56)
for cx,cy,s in [(-.3,.08,1),(.3,.16,.78),(.02,-.3,.85)]:
    for i in range(9):
        a=i*math.tau/9+rng.uniform(-.25,.25)
        card(k,(cx,cy,.01),.35*s,rng.uniform(.62,.80)*s,'fern',yaw=a,tilt=rng.uniform(.45,.80),lean=.04)
objects.append(k.finish(col))
k=Kit('DryScrub'); rng=random.Random(61)
for i in range(7):
    a=i*math.tau/7+.21; base=(rng.uniform(-.1,.1),rng.uniform(-.1,.1),0)
    mid=(math.cos(a)*.25,math.sin(a)*.25,.36); tip=(math.cos(a)*.58,math.sin(a)*.58,rng.uniform(.60,.81))
    branch(k,base,mid,.035,.023); branch(k,mid,tip,.023,.008)
    for j in [-1,1]:
        end=Vector(tip)+Vector((math.cos(a+j*.8)*.16,math.sin(a+j*.8)*.16,-.03))
        branch(k,Vector(mid).lerp(Vector(tip),.55),end,.013,.004)
objects.append(k.finish(col))
k=Kit('FallenBranch'); pts=[(-1,0,.08),(-.4,.07,.12),(.25,-.02,.09),(1,.09,.055)]
for i in range(3): branch(k,pts[i],pts[i+1],.09-i*.019,.07-i*.019)
for i,(a,b) in enumerate([((-.4,.07,.12),(-.1,.53,.15)),((.25,-.02,.09),(.63,-.43,.12)),((-.1,.37,.14),(.22,.50,.19))]): branch(k,a,b,.035,.006)
objects.append(k.finish(col))
k=Kit('RootCluster'); rng=random.Random(66)
for i in range(7):
    a=i*math.tau/7+rng.uniform(-.2,.2); length=rng.uniform(.70,1.05)
    p0=(math.cos(a)*.04,math.sin(a)*.04,.22); p1=(math.cos(a)*.38,math.sin(a)*.38,.16); p2=(math.cos(a)*length,math.sin(a)*length,.045)
    branch(k,p0,p1,.115,.075); branch(k,p1,p2,.075,.012)
    end=(math.cos(a+.3)*length*.92,math.sin(a+.3)*length*.92,.025)
    branch(k,Vector(p1).lerp(Vector(p2),.5),end,.035,.008)
objects.append(k.finish(col))
report={}
for o in objects:
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free()
    lo=Vector(tuple(min(v.co[i] for v in o.data.vertices) for i in range(3))); hi=Vector(tuple(max(v.co[i] for v in o.data.vertices) for i in range(3)))
    off=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v in o.data.vertices: v.co-=off
    report[o.name]={'dimensions_xyz_m':[round(x,4) for x in hi-lo],'triangles':len(o.data.polygons),'materials':[m.name for m in o.data.materials]}
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,bake_anim=False,path_mode='STRIP')
(HERE/'manifest.json').write_text(json.dumps(report,indent=2))
for o,p in zip(objects,[(-2.5,1,0),(0,1,0),(2.5,1,0),(-2.5,-1.4,0),(0,-1.4,0),(2.5,-1.4,0)]): o.location=p
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.world.color=(.20,.20,.20)
bpy.ops.object.light_add(type='AREA',location=(0,-3,8)); bpy.context.object.data.energy=1800; bpy.context.object.data.size=7
bpy.ops.object.camera_add(location=(4,-8,7)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,.15))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=9; scene.camera=cam
scene.render.resolution_x=1800; scene.render.resolution_y=1100; scene.render.resolution_percentage=100; scene.render.filepath=str(HERE/'Vegetation_contact_sheet.png')
for im in bpy.data.images:
    if im.filepath: im.filepath=bpy.path.abspath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Vegetation.blend'))
bpy.ops.render.render(write_still=True)
print(json.dumps(report,indent=2))
