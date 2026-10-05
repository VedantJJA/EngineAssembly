"""Update only sump/timing steps; preserve authored poses and unrelated chapters."""
import copy
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root/'Assets/AssemblyRecipes/three-cylinder.json'
recipe = json.loads(path.read_text(encoding='utf-8-sig'))
parts = recipe['parts']
original_ids = {p['id'] for p in parts}

def match(prefix):
    return [p for p in parts if p['id'].startswith(prefix)]

def stage(items, order, dependencies=(), orientation=0):
    for p in items:
        p.update(order=order, orientation=orientation,
                 prerequisites=sorted({d['id'] for d in dependencies}))

sump = match('304_Oil_Sump_')[0]
baffle = match('303_Oil_Sump_Baffle_')
screws = match('390_Screw_')
members = [sump]+baffle+screws
carrier_id = '380_Complete_Oil_Sump'
carrier = next((p for p in parts if p['id']==carrier_id), None)
if carrier is None:
    origin = copy.deepcopy(sump['targetPosition'])
    carrier = copy.deepcopy(sump)
    carrier.update(id=carrier_id, displayName='Complete oil sump and baffle',
                   isBase=False, subAssemblyId='', parentPartId='', geometryGroup='',
                   targetPosition=origin, targetRotation=dict(x=0,y=0,z=0,w=1))
    for p in members:
        p['subAssemblyId'] = carrier_id
        p['targetPosition'] = {k:p['targetPosition'][k]-origin[k] for k in 'xyz'}
    recipe['subAssemblies'].append(dict(id=carrier_id, rootPartId=carrier_id,
        previewPartId=sump['id'], benchPosition=dict(x=2.4,y=-.25,z=0),
        focusCenter=dict(x=0,y=0,z=0), focusRadius=.65))
    parts.append(carrier)

lower = match('301_'); bed = match('302_')
bedbolts = match('390_Bolt_ISO_4162_M10_x_100')+match('390_Bolt_ISO_4162_M8_x_80')
flybolts = match('390_Bolt_ISO_4017')
sumpbolts = [p for p in match('390_Bolt_') if p not in bedbolts+flybolts]
stage(lower,301,orientation=2); stage(bed,302,lower,2); stage(bedbolts,303,bed,2)
stage([sump],304,bedbolts,1); stage(baffle,305,[sump],1); stage(screws,306,baffle,1)
stage([carrier],307,members+bedbolts,2); stage(sumpbolts,308,[carrier],2)
stage(match('305_'),309,sumpbolts,1); stage(match('306_'),310,match('305_'),1)
stage(flybolts,311,match('306_'),1)
recipe['chapters'][2]['description'] = ('Fit the lower bearings and bedplate. Bench-build the oil sump, '
    'baffle and four screws, then install the complete unit on the inverted engine. '
    'Secure the sump, turn the engine upright and fit the flywheel.')

bearings=match('501_Camshaft'); cams=match('502_'); cover=match('501_Front')
keys=match('503_Camshaft'); hubs=match('504_Camshaft'); washers=match('507_Camshaft')
hubbolts=match('590_Bolt_ISO_4162_M12')
stage(bearings,501); stage(cams,502,bearings); stage(cover,503,cams)
stage(keys,504,cams+cover); stage(hubs,505,keys); stage(washers,506,hubs)
stage(hubbolts,507,washers); stage(match('505_Crankshaft'),508,match('103_Crankshaft'))
stage(match('506_'),509,hubs+hubbolts+match('505_Crankshaft'))
stage(match('507_Crankshaft'),510,match('506_Crankshaft'))
outerbolts=match('590_Bolt_ISO_4162_M8')+match('590_Nut')
stage(outerbolts,511,match('506_')+match('507_Crankshaft'))
mount=match('508_Timing'); stage(mount,512,cover); stage(match('590_Bolt_ISO_4762'),513,mount)
shafts=match('509_Timing'); stage(shafts,514,mount)
innerclips=match('511_Idler_Shaft_Circlip_01')+match('511_Idler_Shaft_Circlip_04')
outerclips=match('511_Idler_Shaft_Circlip_02')+match('511_Idler_Shaft_Circlip_03')
stage(innerclips,515,shafts)
for group in recipe['subAssemblies']:
    if not group['rootPartId'].startswith('530_'):
        continue
    unit = next(p for p in parts if p['id']==group['rootPartId'])
    items = [p for p in parts if p.get('subAssemblyId')==group['id']]
    pulley = [p for p in items if 'Timing_Idler_Pulley' in p['id']]
    bearing = [p for p in items if 'Ball_Bearing' in p['id']]
    clips = [p for p in items if 'Bore_Circlip' in p['id']]
    start = 516+4*(int(group['rootPartId'][-2:])-1)
    stage(pulley,start,innerclips); stage(bearing,start+1,pulley); stage(clips,start+2,bearing)
    stage([unit],530,items+shafts+innerclips)
stage(outerclips,531,match('530_'))
stage(match('512_Timing_Belt'),532,match('530_')+match('511_Idler_Shaft')+outerbolts)
recipe['chapters'][4]['description'] = ('Fit camshafts and keys, then each camshaft hub, inner washer '
    'and hub bolt before its outer pulley. Fit the crankshaft drive and bench-built idlers; belt last.')

# Preserve unordered peer fasteners, but make each preceding operation explicit.
for chapter in ('chapter-3','chapter-5'):
    previous=[]
    for order in sorted({p['order'] for p in parts if p['chapterId']==chapter}):
        peers=[p for p in parts if p['chapterId']==chapter and p['order']==order]
        for p in peers:
            p['prerequisites']=sorted(set(p['prerequisites']+[q['id'] for q in previous]))
        previous=peers
# Valve-stack hints use authored peer order. Reorder only the two edited chapters.
recipe['parts'] = [p for chapter in recipe['chapters'] for p in
    (sorted((p for p in parts if p['chapterId']==chapter['id']), key=lambda p:(p['order'],p['id']))
     if chapter['id'] in ('chapter-3','chapter-5') else
     [p for p in parts if p['chapterId']==chapter['id']])]
assert original_ids <= {p['id'] for p in parts}
assert len({p['id'] for p in parts})==len(parts)
path.write_text(json.dumps(recipe,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(f'Updated sump/timing: {len(parts)} items, {len(recipe["subAssemblies"])} carriers.')
