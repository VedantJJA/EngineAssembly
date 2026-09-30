import bpy,json,os,collections
root=os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
rows=[]
for o in bpy.data.objects:
 if o.type!='MESH': continue
 parent=list(range(len(o.data.vertices)))
 def find(x):
  while parent[x]!=x: parent[x]=parent[parent[x]];x=parent[x]
  return x
 for e in o.data.edges: parent[find(e.vertices[0])]=find(e.vertices[1])
 groups=collections.Counter(find(v.index) for v in o.data.vertices)
 dg=bpy.context.evaluated_depsgraph_get(); ev=o.evaluated_get(dg); mesh=ev.to_mesh()
 rows.append(dict(name=o.name,vertices=len(o.data.vertices),evaluated_vertices=len(mesh.vertices),faces=len(o.data.polygons),components=sorted(groups.values()),modifiers=[dict(name=m.name,type=m.type) for m in o.modifiers],instances=o.instance_type,hidden=o.hide_get(),render_hidden=o.hide_render))
 ev.to_mesh_clear()
label=os.path.basename(bpy.data.filepath)
with open(os.path.join(root,'ModelReview','audit-'+label+'.json'),'w') as f:json.dump(rows,f,indent=2)
print('AUDIT',label,len(rows),sum(r['vertices'] for r in rows),sum(r['evaluated_vertices'] for r in rows))
for r in rows:
 if '1521' in r['name'] or 'Ball_Bearing' in r['name'] or r['modifiers'] or r['vertices']!=r['evaluated_vertices']:print(r)

