"""Compare the current unlabelled CAD source with the preserved source, without modifying either."""
import bpy, json, hashlib, struct
from pathlib import Path
root=Path(__file__).resolve().parents[2]
def inspect(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    output={}
    deps=bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
        digest=hashlib.sha256()
        for vertex in mesh.vertices:digest.update(struct.pack('<3f',*vertex.co))
        for polygon in mesh.polygons:digest.update(struct.pack('<I',len(polygon.vertices)));digest.update(struct.pack('<'+'I'*len(polygon.vertices),*polygon.vertices))
        output[obj.name]={'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'hash':digest.hexdigest(),'matrix':[list(row) for row in obj.matrix_world],'modifiers':[m.type for m in obj.modifiers]}
        evaluated.to_mesh_clear()
    return output
old=inspect(root/'Assets/Model/Edit/3cyl.blend')
new=inspect(root/'Assets/Model/3cyl.blend')
report={'source':'Assets/Model/3cyl.blend','old_objects':len(old),'new_objects':len(new),'added':sorted(new.keys()-old.keys()),'missing':sorted(old.keys()-new.keys()),'changed':{name:{'old':old[name],'new':new[name]} for name in old.keys()&new.keys() if old[name]!=new[name]}}
(root/'ModelReview/corrected-source-comparison.json').write_text(json.dumps(report,indent=2))
print('CORRECTED SOURCE',json.dumps(report))
