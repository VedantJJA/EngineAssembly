import json, csv, copy, math
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Assets/AssemblyRecipes/three-cylinder.json'
backup=root/'ModelReview/three-cylinder-before-subassemblies.json'
if not backup.exists(): backup.write_bytes(path.read_bytes())
recipe=json.loads(backup.read_text(encoding='utf-8-sig'))
recipe['version']=2;recipe['subAssemblies']=[]
parts=recipe['parts'];original_ids={p['id'] for p in parts}
inventory={o['name']:o for o in json.loads((root/'ModelReview/inventory.json').read_text())}
manifest=list(csv.DictReader((root/'ModelReview/parts_manifest.csv').open()))
source={r['object_name']:inventory[r['source_name']] for r in manifest if r['source_name'] in inventory}
def match(text,chapter=None):
 return [p for p in parts if text in p['id'] and (chapter is None or p['chapterId']==f'chapter-{chapter}')]
def stage(items,order,dependencies=(),orientation=0):
 for p in items:
  p['order']=order;p['orientation']=orientation;p['prerequisites']=list(dict.fromkeys([d['id'] if isinstance(d,dict) else d for d in dependencies]))
def group(gid,label,members,preview,order,bench,dependencies=(),orientation=0):
 origin=copy.deepcopy(preview['targetPosition'])
 carrier=copy.deepcopy(preview)
 carrier.update(id=gid,displayName=label,order=order,isBase=False,subAssemblyId='',parentPartId='',geometryGroup='',orientation=orientation,
  targetPosition=origin,targetRotation=dict(x=0,y=0,z=0,w=1),trayRotation=dict(x=0,y=0,z=0,w=1),prerequisites=[m['id'] for m in members]+[p['id'] for p in dependencies])
 for p in members:
  p['subAssemblyId']=gid
  p['targetPosition']={k:p['targetPosition'][k]-origin[k] for k in 'xyz'}
 # Focus around the assembled members' target origins. Editor refinement can use complete mesh bounds.
 points=[p['targetPosition'] for p in members]
 center={k:(min(p[k] for p in points)+max(p[k] for p in points))*.5 for k in 'xyz'}
 radius=max(.35,max(math.sqrt(sum((p[k]-center[k])**2 for k in 'xyz')) for p in points)+.24)
 recipe['subAssemblies'].append(dict(id=gid,rootPartId=gid,previewPartId=preview['id'],benchPosition=dict(zip('xyz',bench)),focusCenter=center,focusRadius=radius))
 parts.append(carrier);return carrier
# Physical parts keep their stable identifiers, but ordering is owned by the recipe.
for p in parts:p['subAssemblyId']='';p['prerequisites']=[];p['parentPartId']=''
block=match('Cylinder_Block')[0];crank=match('103_Crankshaft')[0]
stage([block],101)
stage(match('102_'),102,[block])
stage([crank],103,match('102_'))
piston_units=[]
for cylinder in range(1,4):
 tag=f'Cyl{cylinder:02d}';base=200+(cylinder-1)*10
 piston=match('201_Piston_'+tag);rod=match('202_Connecting_Rod_'+tag);pin=match('203_Piston_Wrist_Pin_'+tag)
 clips=sorted(match('205_Wrist_Pin_Circlip_'+tag),key=lambda p:p['id'])
 studs=match('Stud_JIS_B_1173',2);studs=[p for p in studs if tag in p['id']]
 oil=match('Oil_Control_Ring_'+tag);second=match('Second_Compression_Ring_'+tag);top=match('Top_Compression_Ring_'+tag)
 stage(piston,base+1);stage(clips[:1],base+2,piston);stage(rod,base+3,piston+clips[:1])
 stage(pin,base+4,rod+clips[:1]);stage(clips[1:],base+5,pin)
 stage(studs,base+6,rod);stage(oil,base+7,piston+pin+clips);stage(second,base+8,oil);stage(top,base+9,second)
 members=piston+rod+pin+clips+studs+oil+second+top
 unit=group(f'230_Piston_Rod_Unit_Cyl{cylinder:02d}',f'Piston + rod unit — cylinder {cylinder:02d}',members,piston[0],230,(1.6,.4,(cylinder-2)*.8),[crank],1)
 piston_units.append(unit)
 caps=match('206_Connecting_Rod_Big_End_Cap_'+tag)
 stage(caps,240,[unit],2)
 nuts=[p for p in match('Nut_JIS_B_1170',2) if tag in p['id']]
 stage(nuts,241,caps+studs,2)
recipe['chapters'][1]['description']='Bench-build piston, rod, wrist pin, circlips, studs and rings. Install the three complete units from the cylinder side; turn the block over, then fit the separate rod caps and their nuts.'
# Bottom end: fasten retained hardware before closing the next layer.
lower=match('301_Lower_Main');bed=match('302_Lower');baffle=match('303_Oil');sump=match('304_Oil');fly=match('305_Fly');plate=match('306_Fly')
stage(lower,301,[],2);stage(bed,302,lower,2)
bedbolts=match('390_Bolt_ISO_4162_M10_x_100')+match('390_Bolt_ISO_4162_M8_x_80')
stage(bedbolts,303,bed,2);stage(baffle,304,bedbolts,2)
bafflescrews=match('390_Screw');stage(bafflescrews,305,baffle,2);stage(sump,306,bafflescrews,2)
sumpbolts=[p for p in match('390_') if p not in bedbolts+bafflescrews and 'ISO_4017' not in p['id']]
stage(sumpbolts,307,sump,2);stage(fly,308,sumpbolts,1);stage(plate,309,fly,1);stage(match('390_Bolt_ISO_4017'),310,plate,1)
# Valves are built into the loose head before the complete head reaches the engine.
head=match('401_Cylinder_Head')[0]
headmembers=[p for p in parts if p['chapterId']=='chapter-4' and not p['id'].startswith('490_')]
for order in range(401,408):
 stage([p for p in headmembers if p['order']==order],order,[p for p in headmembers if p['order']==order-1])
headunit=group('480_Complete_Cylinder_Head','Complete cylinder head and valves',headmembers,head,480,(1.7,.6,0),[],1)
stage(match('490_'),490,[headunit],1)
recipe['chapters'][3]['description']='Assemble the valves, springs, retainers and split keepers in the loose cylinder head. Install the complete head, then secure the head bolts.'
# Front/timing drive: shafts, keys, hubs, washers and their retaining bolts before the belt.
bearings=match('501_Camshaft');cams=match('502_');cover=match('501_Front')
stage(bearings,501);stage(cams,502,bearings);stage(cover,503,cams)
stage(match('503_Camshaft'),504,cams)
stage(match('504_Camshaft'),505,match('503_Camshaft'))
stage(match('590_Bolt_ISO_4162_M12'),506,match('504_Camshaft'))
stage(match('505_Crankshaft'),507,[crank])
stage(match('506_'),508,match('504_Camshaft')+match('505_Crankshaft'))
stage(match('507_'),509,match('506_'))
stage(match('590_Bolt_ISO_4162_M8')+match('590_Nut'),510,match('507_'))
mount=match('508_Timing');stage(mount,511,cover)
stage(match('590_Bolt_ISO_4762'),512,mount);stage(match('509_Timing'),513,mount)
# Idler pulleys receive their internal bearings and bore retaining rings off the engine.
def center(p):return source[p['id']]['center']
def nearest(item,options):return min(options,key=lambda p:sum((a-b)**2 for a,b in zip(center(item),center(p))))
timing_units=[]
pulleys=match('511_Timing_Idler_Pulley')
for i,pulley in enumerate(pulleys):
 bearing=[p for p in match('510_Idler_Ball') if nearest(p,pulleys)==pulley]
 clips=[p for p in match('511_Idler_Bore') if nearest(p,pulleys)==pulley]
 shaft=min(match('509_Timing'),key=lambda p:sum((a-b)**2 for a,b in zip(center(p),center(pulley))))
 base=514+i*4
 stage([pulley],base);stage(bearing,base+1,[pulley]);stage(clips,base+2,bearing)
 unit=group(f'530_Timing_Idler_Unit_{i+1:02d}',f'Timing idler unit {i+1:02d}',[pulley]+bearing+clips,pulley,530,(1.6,.4,i*.6),[shaft])
 timing_units.append(unit)
stage(match('511_Idler_Shaft'),531,timing_units)
stage(match('512_Timing_Belt'),532,timing_units+match('511_Idler_Shaft')+match('590_Bolt_ISO_4162_M8')+match('590_Nut'))
# Head cover precedes its screws; each later service fitting waits for its own supporting part.
stage(match('601_'),601,cams);stage(match('690_Bolt_ISO_4162'),602,match('601_'))
stage(match('602_Fuel'),603,match('601_'));stage(match('603_Fuel'),604,match('602_Fuel'))
stage(match('690_Bolt_ISO_4762_M6'),605,match('603_Fuel'));stage(match('604_Fuel'),606,match('603_Fuel'))
stage(match('605_Crankcase'),607,match('601_'));stage(match('690_Bolt_ISO_4762_M8'),608,match('605_Crankcase'))
stage(match('606_Breather'),609,match('605_Crankcase'))
# Turbo bearings precede shaft/wheels; the outlet must be present before its band clamp.
stage(match('701_'),701);stage(match('790_Bolt_ISO_4762_M6'),702,match('701_'))
stage(match('703_Turbo_Shaft_Bearing'),703,match('701_'));stage(match('702_Turbo'),704,match('703_Turbo_Shaft_Bearing'))
stage(match('703_Turbo_Shaft_Circlip'),705,match('702_Turbo'));stage(match('704_'),706,match('702_Turbo')+match('703_Turbo_Shaft_Circlip'))
stage(match('790_Bolt_ISO_4762_M4')+match('790_Bolt_ISO_4762_M8'),707,match('704_'))
stage(match('706_'),708,match('701_'));stage(match('705_'),709,match('706_'))
stage(match('801_'),801);stage(match('802_'),802,match('801_'));stage(match('803_'),803,match('802_'))
# Accessory supports and pulley insert assembly; keep the belt last.
stage(match('901_'),901);stage(match('902_'),902,match('901_'))
stage(match('903_')+match('904_'),903,match('901_')+match('902_'));stage(match('905_'),904,match('903_'))
stage(match('906_'),905,match('901_'))
pulley=match('907_Accessory_Idler_Pulley')[0];bearing=match('907_Idler_Ball');clips=match('907_Idler_Bore')
stage([pulley],906);stage(bearing,907,[pulley]);stage(clips,908,bearing)
unit=group('909_Accessory_Idler_Unit','Accessory idler unit',[pulley]+bearing+clips,pulley,909,(1.6,.4,0),match('906_'))
stage(match('907_Idler_Shaft'),910,[unit]);stage(match('908_Motor'),911,match('901_'))
stage(match('990_Bolt'),912,match('908_Motor'));stage(match('990_Nut'),913,match('990_Bolt'))
stage(match('909_Motor_Wiring'),914,match('908_Motor')+match('990_Nut'))
stage(match('910_Accessory_Drive_Belt'),915,[unit]+match('905_')+match('908_Motor')+match('990_Nut'))
# Save deterministic explicit earlier-step prerequisites: no sibling bolt-by-bolt ordering.
for chapter in recipe['chapters']:
 groups={}
 for p in parts:
  if p['chapterId']==chapter['id']:groups.setdefault(p['order'],[]).append(p)
 previous=[]
 for order in sorted(groups):
  current=groups[order]
  for p in current:p['prerequisites']=sorted(set(p['prerequisites']+[d['id'] for d in previous]))
  previous=current
parts.sort(key=lambda p:(recipe['chapters'].index(next(c for c in recipe['chapters'] if c['id']==p['chapterId'])),p['order'],p['id']))
assert original_ids=={p['id'] for p in parts if p['id'] in original_ids}
assert len(parts)==318+len(recipe['subAssemblies'])
path.write_text(json.dumps(recipe,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
report={'physicalParts':318,'carrierSteps':len(recipe['subAssemblies']),'steps':len(parts),'groups':recipe['subAssemblies'],
 'basis':'CAD part geometry, source placements, separate caps and clips; proposed educational workflow, not an OEM service procedure.'}
(root/'ModelReview/assembly-order-review.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(f'Revised recipe: {len(parts)} steps, {len(recipe["subAssemblies"])} subassemblies; every original part retained.')

