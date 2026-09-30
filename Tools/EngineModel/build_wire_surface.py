"""Give the preserved CAD wire centre-lines a renderable surface; never modify the source .blend."""
import bpy
import os
root=os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,'Assets/Model/Edit/3cyl_Labeled.blend'))
wire=bpy.data.objects['909_Motor_Wiring_01']
assert len(wire.data.polygons)==0
adj={v.index:[] for v in wire.data.vertices}
for edge in wire.data.edges:
    a,b=edge.vertices;adj[a].append(b);adj[b].append(a)
curve=bpy.data.curves.new('CAD wire routing surface','CURVE');curve.dimensions='3D'
# CAD physical units are uncalibrated. This is a visual sleeve around the exact source route.
curve.bevel_depth=.002;curve.bevel_resolution=2;curve.use_fill_caps=True;curve.resolution_u=1
remaining=set(adj)
while remaining:
    start=next((i for i in remaining if len(adj[i])==1),next(iter(remaining)))
    route=[];current=start
    while current in remaining:
        route.append(current);remaining.remove(current)
        current=next((i for i in adj[current] if i in remaining),-1)
    spline=curve.splines.new('POLY');spline.points.add(len(route)-1)
    for point,index in zip(spline.points,route):point.co=(*wire.data.vertices[index].co,1)
surface=bpy.data.objects.new('909_Motor_Wiring_Surface',curve);bpy.context.scene.collection.objects.link(surface)
surface.matrix_world=wire.matrix_world
for material in wire.data.materials:curve.materials.append(material)
bpy.ops.object.select_all(action='DESELECT');surface.select_set(True);bpy.context.view_layer.objects.active=surface
bpy.ops.object.convert(target='MESH')
output=os.path.join(root,'Assets/Model/Edit/909_Motor_Wiring_Surface.fbx')
bpy.ops.export_scene.fbx(filepath=output,use_selection=True,object_types={'MESH'},global_scale=1.0,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=False)
print('WIRE SURFACE',len(surface.data.vertices),len(surface.data.polygons),output)
