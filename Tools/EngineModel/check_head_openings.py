"""Probe both head revisions in Blender against the actual seat/seal surfaces and valve axes."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(__file__).resolve().parents[2]
def inspect(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    head=bpy.data.objects['401_Cylinder_Head_01']
    tree=BVHTree.FromPolygons([head.matrix_world@v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    valves=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('404_')]
    centre=lambda o:sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8
    result=[]
    for part in sorted([o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith(('402_','403_'))],key=lambda o:o.name):
        valve=min(valves,key=lambda o:(centre(o)-centre(part)).length)
        extents=[max(v.co[i] for v in valve.data.vertices)-min(v.co[i] for v in valve.data.vertices) for i in range(3)]
        local=Vector();local[max(range(3),key=lambda i:extents[i])]=1
        axis=(valve.matrix_world.to_3x3()@local).normalized()
        if axis.z<0:axis=-axis
        tangent=axis.cross(Vector((1,0,0))).normalized();bitangent=axis.cross(tangent).normalized()
        samples=[part.matrix_world@p.center for p in list(part.data.polygons)[::max(1,len(part.data.polygons)//128)]]
        good=0;blocked_faces=set();total=0
        for direction in [axis]+[(axis+(tangent*math.cos(i*math.pi/4)+bitangent*math.sin(i*math.pi/4))*.35).normalized() for i in range(8)]:
            for point in samples:
                total+=1;hit,normal,face,distance=tree.ray_cast(point+direction*2,-direction,2-.0001)
                if hit is None:good+=1
                else:blocked_faces.add(face)
        result.append({'part':part.name,'visible_surface_probes':good,'total':total,'blocking_faces':len(blocked_faces)})
    return result
old=inspect(root/'Tools/Verification/BeforeHeadGeometryFix/3cyl_Labeled.blend')
new=inspect(root/'Assets/Model/Edit/3cyl_Labeled.blend')
report={'old':old,'corrected':new,'corrected_all_seats_and_seals_accessible':all(p['visible_surface_probes']>0 for p in new)}
(root/'ModelReview/head-openings-review.json').write_text(json.dumps(report,indent=2))
print('HEAD OPENINGS',json.dumps(report))
