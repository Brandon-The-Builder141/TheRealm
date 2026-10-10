"""Deterministic ground overlays; run Blender --background --disable-autoexec --python this_file."""
import bpy, math, json
import numpy as np
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
OUT=ROOT/'Assets/Realm/Art/World/Pass5/Ground'
TEX=OUT/'Textures'; TEX.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'Assets/Realm/Art/World/Textures'
bpy.ops.wm.read_factory_settings(use_empty=True)
N=512
yy,xx=np.mgrid[0:N,0:N]; u=(xx+.5)/N; v=(yy+.5)/N
x=(u-.5)*2; y=(v-.5)*2; radius=np.sqrt(x*x+y*y); angle=np.arctan2(y,x)
assets=[]; report=[]
specs=[('ForestFloorPatch','P5_ForestFloor','W_ForestFloor',4051),('GravelPatch','P5_Gravel','W_Rock',4052),('MossPatch','P5_Moss','W_Grass',4053)]
for name,matname,source,seed in specs:
    rng=np.random.default_rng(seed)
    im=bpy.data.images.load(str(SOURCE/(source+'_BaseColor.png')),check_existing=True)
    sw,sh=im.size; pixels=np.empty(sw*sh*4,np.float32); im.pixels.foreach_get(pixels); pixels=pixels.reshape(sh,sw,4)
    # Six meters span UV 0..1; 4m source tile repeats 1.5 times.
    sx=((u*1.5+.173)*sw).astype(int)%sw; sy=((v*1.5+.381)*sh).astype(int)%sh
    rgba=pixels[sy,sx].copy()
    organic=.84+.065*np.sin(angle*3+seed)+.035*np.cos(angle*7+seed*.7)+.018*np.sin(angle*11)
    edge=radius/organic
    probability=np.clip((1-edge)/.29,0,1)
    probability=probability*probability*(3-2*probability)
    # Binary blue-ish fine grain survives HDRP alpha clipping; solid inner 70%.
    rgba[:,:,3]=(rng.random((N,N))<probability).astype(np.float32)
    tex=bpy.data.images.new(matname+'_BaseColor',width=N,height=N,alpha=True)
    tex.pixels.foreach_set(rgba.ravel()); tex.filepath_raw=str(TEX/(matname+'_BaseColor.png')); tex.file_format='PNG'; tex.save()
    m=bpy.data.materials.new(matname); m.use_nodes=True
    nt=m.node_tree; nt.nodes.clear(); output=nt.nodes.new('ShaderNodeOutputMaterial')
    bs=nt.nodes.new('ShaderNodeBsdfPrincipled'); bs.inputs['Roughness'].default_value=.95
    img=nt.nodes.new('ShaderNodeTexImage'); img.image=tex
    nt.links.new(img.outputs['Color'],bs.inputs['Base Color'])
    # Explicit threshold preview matches Unity's requested cutoff .4.
    cut=nt.nodes.new('ShaderNodeMath'); cut.operation='GREATER_THAN'; cut.inputs[1].default_value=.4
    nt.links.new(img.outputs['Alpha'],cut.inputs[0])
    transparent=nt.nodes.new('ShaderNodeBsdfTransparent'); mix=nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(cut.outputs[0],mix.inputs[0]); nt.links.new(transparent.outputs[0],mix.inputs[1]); nt.links.new(bs.outputs[0],mix.inputs[2]); nt.links.new(mix.outputs[0],output.inputs[0])
    verts=[(0,0,.015)]
    for i in range(64):
        a=i*math.tau/64; r=2.96+.025*math.sin(a*7+seed)
        verts.append((math.cos(a)*r,math.sin(a)*r,.015))
    faces=[(0,i+1,(i+1)%64+1) for i in range(64)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.materials.append(m)
    uv=mesh.uv_layers.new(name='UVMap')
    for p in mesh.polygons:
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co; uv.data[li].uv=(co.x/6+.5,co.y/6+.5)
    ob=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(ob); assets.append(ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active=ob
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,bake_anim=False,path_mode='STRIP')
    report.append({'name':name,'material':matname,'source_texture':source+'_BaseColor.png','triangles':64,'footprint_m':5.97,'plane_z_m':.015,'source_tile_m':4,'texture_size':N,'alpha_cutoff':.4,'collider':False})
(HERE/'asset_report.json').write_text(json.dumps(report,indent=2))
bpy.data.texts.new('build_ground.py').write(Path(__file__).read_text())
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Ground.blend'))
for ob,xpos in zip(assets,[-6.5,0,6.5]): ob.location.x=xpos
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0)); base=bpy.context.object
mat=bpy.data.materials.new('Preview neutral earth'); mat.diffuse_color=(.18,.145,.10,1); base.data.materials.append(mat)
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.render.resolution_x=1600; scene.render.resolution_y=650; scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('PreviewWorld'); scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.4,.4,.4,1)
bpy.ops.object.camera_add(location=(0,-9,17)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=20.5; scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-3,-4,10)); bpy.context.object.data.energy=1500; bpy.context.object.data.size=10
for ob in assets:
    bpy.ops.object.text_add(location=(ob.location.x-2,-3.35,.025)); label=bpy.context.object; label.data.body=ob.name; label.data.size=.38
scene.render.filepath=str(HERE/'Ground_ContactSheet.png'); bpy.ops.render.render(write_still=True)
print(json.dumps(report))
