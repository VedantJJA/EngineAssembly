import bpy,os,json,csv,collections
from mathutils import Vector,kdtree
root=os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
asset=os.path.join(root,'Assets','Model','Edit');review=os.path.join(root,'ModelReview')
bpy.ops.wm.open_mainfile(filepath=os.path.join(asset,'3cyl.blend'))
source={o.name:dict(points=[o.matrix_world@v.co for v in o.data.vertices],faces=len(o.data.polygons)) for o in bpy.context.scene.objects if o.type=='MESH'}
bpy.ops.wm.open_mainfile(filepath=os.path.join(asset,'3cyl_Labeled.blend'))
with open(os.path.join(review,'parts_manifest.csv'),encoding='utf-8') as f:rows=list(csv.DictReader(f))
assert set(source)==set(r['source_name'] for r in rows)
targets={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
assert set(targets)==set(r['object_name'] for r in rows)
trees={}
for name,o in targets.items():
 tree=kdtree.KDTree(len(o.data.vertices))
 for i,v in enumerate(o.data.vertices):tree.insert(o.matrix_world@v.co,i)
 tree.balance();trees[name]=tree
checks=[]
for r in rows:
 original=source[r['source_name']]
 error=max(trees[r['object_name']].find(p)[2] for p in original['points'])
 assert error<1e-5,(r['source_name'],error)
 checks.append(dict(source=r['source_name'],target=r['object_name'],vertices=len(original['points']),max_world_error=error))
expected={n:dict(points=[o.matrix_world@v.co for v in o.data.vertices],faces=len(o.data.polygons)) for n,o in targets.items()}
bearings=[]
for n,o in targets.items():
 if 'Ball_Bearing' not in n:continue
 parent=list(range(len(o.data.vertices)))
 def find(x):
  while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
  return x
 for edge in o.data.edges:parent[find(edge.vertices[0])]=find(edge.vertices[1])
 islands=collections.defaultdict(list)
 for v in o.data.vertices:islands[find(v.index)].append(o.matrix_world@v.co)
 components=[]
 for pts in islands.values():
  lo=Vector(tuple(min(p[j] for p in pts) for j in range(3)));hi=Vector(tuple(max(p[j] for p in pts) for j in range(3)))
  components.append(dict(vertices=len(pts),world_center=list((hi+lo)*.5),bounds_size=list(hi-lo)))
 bearings.append(dict(name=n,connected_components=components))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(asset,'3cyl_Labeled.fbx'))
exported={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
assert set(exported)==set(expected)
fbxmax=0
for name,o in exported.items():
 assert len(o.data.vertices)==len(expected[name]['points'])
 assert len(o.data.polygons)==expected[name]['faces']
 tree=kdtree.KDTree(len(o.data.vertices))
 for i,v in enumerate(o.data.vertices):tree.insert(o.matrix_world@v.co,i)
 tree.balance()
 error=max(tree.find(p)[2] for p in expected[name]['points'])
 assert error<1e-5,(name,error)
 fbxmax=max(fbxmax,error)
report=dict(passed=True,source_meshes=len(source),labeled_parts=len(expected),source_vertices=sum(len(s['points']) for s in source.values()),labeled_vertices=sum(len(s['points']) for s in expected.values()),source_faces=sum(s['faces'] for s in source.values()),labeled_faces=sum(s['faces'] for s in expected.values()),all_original_objects_accounted_for=True,all_source_vertices_preserved=True,fbx_max_world_error=fbxmax,only_merge='113 GSwirl source objects form one motor',bearings=bearings,parts=checks)
with open(os.path.join(review,'geometry_audit.json'),'w') as f:json.dump(report,f,indent=2)
print('PASS',report['source_meshes'],report['labeled_parts'],report['source_vertices'],report['source_faces'],'FBX max error',fbxmax)
print('BEARINGS',bearings[:1])

