"""Render both head revisions from the same valve-side camera, without saving model changes."""
import bpy
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[2]
for revision,path in [('before',root/'Tools/Verification/BeforeHeadGeometryFix/3cyl_Labeled.blend'),('corrected',root/'Assets/Model/Edit/3cyl_Labeled.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    head=bpy.data.objects['401_Cylinder_Head_01']
    valves=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('404_')]
    axis=Vector()
    for valve in valves:
        extents=[max(v.co[i] for v in valve.data.vertices)-min(v.co[i] for v in valve.data.vertices) for i in range(3)]
        direction=Vector();direction[max(range(3),key=lambda i:extents[i])]=1
        direction=(valve.matrix_world.to_3x3()@direction).normalized()
        if direction.z<0:direction=-direction
        axis+=direction
    axis.normalize()
    corners=[head.matrix_world@Vector(c) for c in head.bound_box]
    centre=sum(corners,Vector())/8
    for obj in bpy.context.scene.objects:obj.hide_render=obj!=head
    data=bpy.data.cameras.new('Geometry review camera')
    camera=bpy.data.objects.new('Geometry review camera',data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location=centre+axis*3
    camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=max(max(c[i] for c in corners)-min(c[i] for c in corners) for i in range(3))*1.15
    scene=bpy.context.scene;scene.camera=camera
    scene.render.engine='BLENDER_WORKBENCH'
    scene.display.shading.light='STUDIO'
    scene.display.shading.color_type='SINGLE'
    scene.display.shading.single_color=(.62,.65,.7)
    scene.display.shading.show_cavity=True
    scene.display.shading.cavity_type='BOTH'
    scene.display.shading.background_type='WORLD'
    scene.world.color=(.04,.04,.04)
    scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(root/'ModelReview'/('head-'+revision+'.png'))
    bpy.ops.render.render(write_still=True)
