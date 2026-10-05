import bpy,json
from pathlib import Path
from mathutils import kdtree
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'Assets/Model/Edit/3cyl_Labeled.blend'))
expected={}
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    obj.data.calc_loop_triangles()
    expected[obj.name]={'points':[obj.matrix_world@v.co for v in obj.data.vertices],'triangles':len(obj.data.loop_triangles),'parent':obj.parent.name if obj.parent else None}
assert len(expected)==318
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Model/Edit/3cyl_Labeled.fbx'))
actual={obj.name:obj for obj in bpy.context.scene.objects if obj.type=='MESH'}
assert actual.keys()==expected.keys()
maximum=0
for name,obj in actual.items():
    record=expected[name];obj.data.calc_loop_triangles()
    assert (obj.parent.name if obj.parent else None)==record['parent'],name
    assert len(obj.data.loop_triangles)==record['triangles'],(name,len(obj.data.loop_triangles),record['triangles'])
    tree=kdtree.KDTree(len(obj.data.vertices))
    for i,vertex in enumerate(obj.data.vertices):tree.insert(obj.matrix_world@vertex.co,i)
    tree.balance();error=max(tree.find(point)[2] for point in record['points']);assert error<1e-5,(name,error);maximum=max(maximum,error)
report={'passed':True,'labelled_parts':318,'all_names_and_parents_preserved':True,'all_triangle_surfaces_preserved':True,'max_fbx_world_error':maximum,'corrected_head_triangles':expected['401_Cylinder_Head_01']['triangles']}
(root/'ModelReview/corrected-fbx-verification.json').write_text(json.dumps(report,indent=2));print('CORRECTED FBX VERIFY',json.dumps(report))
