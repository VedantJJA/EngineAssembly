import bpy, os, json
from mathutils import Vector, kdtree
ROOT=os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
review=os.path.join(ROOT,'ModelReview'); asset=os.path.join(ROOT,'Assets','Model','Edit')
bpy.ops.wm.open_mainfile(filepath=os.path.join(asset,'3cyl.blend'))
original_points=[o.matrix_world @ v.co for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('asdasdasdasdasdasd-1_') for v in o.data.vertices]
bpy.ops.wm.open_mainfile(filepath=os.path.join(asset,'3cyl_Labeled.blend'))
motor=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('merged_source_count')==113)
tree=kdtree.KDTree(len(motor.data.vertices))
for i,v in enumerate(motor.data.vertices):tree.insert(motor.matrix_world @ v.co,i)
tree.balance()
motor_error=max(tree.find(p)[2] for p in original_points)
assert motor_error<1e-5,motor_error
expected={o.name:dict(parent=o.parent.name if o.parent else None,vertices=len(o.data.vertices) if o.type=='MESH' else 0) for o in bpy.context.scene.objects}
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.image_settings.file_format='PNG'
scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH';scene.display.shading.background_type='WORLD';scene.world.color=(.8,.8,.8)
data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(cam)
cam.location=(4,5,3);cam.rotation_euler=(Vector((0,-.55,.4))-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=3.6;scene.camera=cam
scene.render.resolution_x=1400;scene.render.resolution_y=1400;scene.render.resolution_percentage=100;scene.render.filepath=os.path.join(review,'labeled_overview.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(asset,'3cyl_Labeled.fbx'))
actual={o.name:dict(parent=o.parent.name if o.parent else None,vertices=len(o.data.vertices) if o.type=='MESH' else 0) for o in bpy.context.scene.objects}
assert expected.keys()==actual.keys(),(expected.keys()-actual.keys(),actual.keys()-expected.keys())
assert all(actual[n]['parent']==v['parent'] for n,v in expected.items())
assert all(actual[n]['vertices']==v['vertices'] for n,v in expected.items())
path=os.path.join(review,'validation.json')
with open(path) as f:report=json.load(f)
report.update(motor_max_vertex_position_error=motor_error,fbx_roundtrip_object_count=len(actual),fbx_names_and_parents_preserved=True,fbx_vertex_counts_preserved=True)
with open(path,'w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
