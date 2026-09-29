import bpy, os, math
from mathutils import Vector, Matrix
out=os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))),'ModelReview')
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.render.image_settings.file_format='PNG'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH'
scene.display.shading.background_type='WORLD'
scene.world.color=(0.8,0.8,0.8)
meshes=[o for o in scene.objects if o.type=='MESH']
def camera(loc,target,scale):
    data=bpy.data.cameras.new('ReviewCamera'); ob=bpy.data.objects.new('ReviewCamera',data); scene.collection.objects.link(ob)
    ob.location=loc; ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler(); data.type='ORTHO'; data.ortho_scale=scale; scene.camera=ob
    return ob
cam=camera((4,5,3),(0,-.55,.4),3.6)
scene.render.resolution_x=1400; scene.render.resolution_y=1400; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(out,'original_overview.png'); bpy.ops.render.render(write_still=True)
for o in meshes: o.hide_render=True
rot=Vector((3,4,2.5)).to_track_quat('Z','Y').to_matrix().transposed().to_4x4()
for n in range(1,60):
    src=bpy.data.objects.get('Part'+str(n))
    if not src: continue
    ob=bpy.data.objects.new(src.name+'_preview',src.data.copy()); scene.collection.objects.link(ob)
    transform=rot @ src.matrix_world
    coords=[transform @ v.co for v in ob.data.vertices]
    lo=Vector(tuple(min(v[a] for v in coords) for a in range(3))); hi=Vector(tuple(max(v[a] for v in coords) for a in range(3)))
    center=(lo+hi)/2; scale=1.65/max(hi.x-lo.x,hi.y-lo.y)
    col=(n-1)%6; row=(n-1)//6; offset=Vector((col*2.2,-row*2.35,0))
    for v,p in zip(ob.data.vertices,coords): v.co=(p-center)*scale+offset
    t=bpy.data.curves.new('label','FONT'); t.body='Part'+str(n); t.size=.18; t.align_x='CENTER'
    label=bpy.data.objects.new('label',t); scene.collection.objects.link(label); label.location=(offset.x,offset.y-1.08,1)
cam.location=(5.5,-10.5,50);cam.rotation_euler=(0,0,0);cam.data.ortho_scale=24
scene.render.resolution_x=1800;scene.render.resolution_y=3000
scene.render.filepath=os.path.join(out,'parts_contact_sheet.png'); bpy.ops.render.render(write_still=True)
