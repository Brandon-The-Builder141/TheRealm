import bpy, json
from pathlib import Path
here=Path(__file__).resolve().parent
root=here.parents[3]/'Assets/Realm/Art/World/Pass4/Nature'
results=[]
for p in sorted(root.glob('*.fbx')):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(p))
    o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    assert len(o.data.uv_layers)==1
    assert all(len(f.vertices)==3 for f in o.data.polygons)
    assert all(v.co.length<30 for v in o.data.vertices)
    results.append(dict(name=o.name,vertices=len(o.data.vertices),triangles=len(o.data.polygons),uv_layers=len(o.data.uv_layers),dimensions=list(o.dimensions)))
assert len(results)==5
(here/'validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
