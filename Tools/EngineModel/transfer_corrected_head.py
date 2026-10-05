"""Transfer the corrected CAD head into the existing labelled asset; retain all names and poses."""
import bpy,json,csv,hashlib,struct
from pathlib import Path
root=Path(__file__).resolve().parents[2]
asset=root/'Assets/Model/Edit'
head_name='401_Cylinder_Head_01'
bpy.ops.wm.open_mainfile(filepath=str(asset/'3cyl_Labeled.blend'))
before={o.name:{'matrix':[list(row) for row in o.matrix_world],'vertices':len(o.data.vertices),'faces':len(o.data.polygons)} for o in bpy.context.scene.objects if o.type=='MESH'}
assert len(before)==318
head=bpy.data.objects[head_name]
old_data=head.data;materials=list(old_data.materials);data_name=head_name+'_Mesh'
with bpy.data.libraries.load(str(root/'Assets/Model/3cyl.blend'),link=False) as (source,destination):
    assert 'Part16' in source.objects
    destination.objects=['Part16']
corrected=destination.objects[0]
assert not corrected.modifiers, 'Evaluate source modifiers explicitly before transfer.'
head.data=corrected.data.copy()
if old_data.users==0:bpy.data.meshes.remove(old_data)
head.data.name=data_name
head.data.materials.clear()
for material in materials:head.data.materials.append(material)
bpy.data.objects.remove(corrected,do_unlink=True)
for name,record in before.items():
    obj=bpy.data.objects[name]
    assert [list(row) for row in obj.matrix_world]==record['matrix'],name
    if name!=head_name:assert len(obj.data.vertices)==record['vertices'] and len(obj.data.polygons)==record['faces'],name
head['geometry_revision']='Corrected Part16 from Assets/Model/3cyl.blend; existing assembly transform retained.'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(asset/'3cyl_Labeled.blend'))
bpy.ops.export_scene.fbx(filepath=str(asset/'3cyl_Labeled.fbx'),use_selection=False,object_types={'EMPTY','MESH'},global_scale=1.0,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,use_custom_props=True,path_mode='AUTO')
baseline=json.loads((root/'ModelReview/corrected-source-comparison.json').read_text())['changed']['Part16']['old']['faces']
report={'passed':True,'fixed_source':'Assets/Model/3cyl.blend','changed_parts':[head_name],'parts':318,'original_faces':baseline,'corrected_faces':len(head.data.polygons),'all_labels_preserved':True,'all_assembly_transforms_preserved':True,'motor_and_bearings_preserved':True}
(root/'ModelReview/corrected-head-transfer.json').write_text(json.dumps(report,indent=2))
print('CORRECTED HEAD TRANSFER',json.dumps(report))
