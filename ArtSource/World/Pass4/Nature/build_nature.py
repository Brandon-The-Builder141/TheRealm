"""Run Blender --background --disable-autoexec --python build_nature.py."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[3]
SOURCE = HERE.parents[1] / 'RealmWorld.blend'
OUT = PROJECT / 'Assets/Realm/Art/World/Pass4/Nature'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
exec(bpy.data.texts['world_lib.py'].as_string(), globals())
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
col = collection('Pass4_Nature')
random.seed(4401)

def stone(kit, center, size, seed, mat='W_Rock', rings=5):
    """Closed angular bedded outcrop; staggered ring lips read as erosion strata."""
    rng=random.Random(seed); n=12; verts=[]; faces=[]
    radial=[rng.uniform(.87,1.12) for _ in range(n)]
    for j in range(rings):
        t=j/(rings-1); lip=[.88,1.03,.87,.99,.73][j%5]
        for i in range(n):
            a=2*math.pi*i/n
            z=t*size[2] + (rng.uniform(-.1,.1)*size[2] if j==rings-1 else 0)
            verts.append(Vector((center[0]+math.cos(a)*size[0]/2*radial[i]*lip + t*.15*size[0],center[1]+math.sin(a)*size[1]/2*radial[i]*lip,z+center[2])))
    faces.append(tuple(range(n))[::-1]); faces.append(tuple(range((rings-1)*n,rings*n)))
    for j in range(rings-1):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    kit.add_geom((verts,faces),mat,uv_offset=Vector((seed%7,seed%11)))

objects=[]
for k,(name,w,d,h) in enumerate([('Cliff_A',8,5,8),('Cliff_B',9,5.5,10),('Cliff_C',7,4.5,6.5)]):
    kit=Kit(name)
    stone(kit,(0,0,0),(w*.82,d,h),100+k)
    stone(kit,(-w*.29,-d*.14,0),(w*.48,d*.72,h*.70),200+k)
    stone(kit,(w*.29,-d*.16,0),(w*.40,d*.68,h*.47),300+k)
    for i in range(5):
        stone(kit,(-w*.35+i*w*.16,-d*.37,0),(w*.24,d*.30,.7+i%3*.3),400+k*10+i,'W_Rubble',3)
    objects.append(kit.finish(col))
kit=Kit('RockShelf')
stone(kit,(0,0,0),(6,3.8,1.5),501)
stone(kit,(-1,.1,.7),(3.5,2.8,1.1),502)
objects.append(kit.finish(col))
kit=Kit('ScreeCluster')
for i in range(22):
    r=random.Random(600+i); a=r.uniform(0,math.tau); rad=r.uniform(.1,1.9); s=r.uniform(.25,.9)
    stone(kit,(math.cos(a)*rad,math.sin(a)*rad*.6,0),(s,s*.8,s*.6),600+i,'W_Rubble',3)
objects.append(kit.finish(col))
stats={}
for o in objects:
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm,faces=list(bm.faces)); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free()
    coords=[v.co for v in o.data.vertices]; lo=Vector(tuple(min(v[i] for v in coords) for i in range(3))); hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v in o.data.vertices: v.co-=offset
    stats[o.name]={'dimensions_xyz_m':[round(x,3) for x in hi-lo],'triangles':len(o.data.polygons),'materials':[m.name for m in o.data.materials]}
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/(o.name+'.fbx')),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,bake_anim=False,path_mode='STRIP')
(HERE/'manifest.json').write_text(json.dumps(stats,indent=2))
# Save a clean source with assets spaced for inspection. Exports above have identity transforms.
for o,pos in zip(objects,[(-12,0,0),(0,0,0),(12,0,0),(-6,-9,0),(6,-9,0)]): o.location=pos
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.world.color=(.18,.18,.18)
bpy.ops.object.light_add(type='AREA',location=(0,-12,22)); bpy.context.object.data.energy=6500; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=18
bpy.ops.object.light_add(type='AREA',location=(10,8,18)); bpy.context.object.data.energy=4500; bpy.context.object.data.size=15
bpy.ops.object.camera_add(location=(24,-43,30)); camera=bpy.context.object; camera.rotation_euler=(Vector((0,-1,3))-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=41; scene.camera=camera
scene.render.resolution_x=1800; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(HERE/'Nature_contact_sheet.png')
# Resolve original texture paths before saving in a deeper directory.
for im in bpy.data.images:
    if im.filepath: im.filepath=bpy.path.abspath(im.filepath)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Nature.blend'))
bpy.ops.render.render(write_still=True)
print(json.dumps(stats,indent=2))
