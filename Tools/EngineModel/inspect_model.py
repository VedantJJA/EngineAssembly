import bpy, json, os
from mathutils import Vector
out = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), 'ModelReview')
rows=[]
for i,o in enumerate(sorted(bpy.data.objects,key=lambda o:o.name)):
    corners=[o.matrix_world @ Vector(c) for c in o.bound_box] if o.type=='MESH' else []
    rows.append(dict(id=i,name=o.name,type=o.type,parent=o.parent.name if o.parent else None, vertices=len(o.data.vertices) if o.type=='MESH' else 0, materials=[m.name if m else None for m in o.data.materials] if o.type=='MESH' else [], center=list(sum(corners,Vector())/8) if corners else list(o.location),dimensions=list(o.dimensions), collections=[c.name for c in o.users_collection]))
with open(os.path.join(out,'inventory.json'),'w') as f: json.dump(rows,f,indent=2)
print('INVENTORY',len(rows))
