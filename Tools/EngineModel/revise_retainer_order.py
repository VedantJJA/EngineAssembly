"""Apply requested retainer/accessory sequence changes without changing CAD poses."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / 'Assets/AssemblyRecipes/three-cylinder.json'
recipe = json.loads(path.read_text(encoding='utf-8-sig'))
removed = '909_Motor_Wiring_01'
parts = recipe['parts'] = [p for p in recipe['parts'] if p['id'] != removed]

def match(prefix):
    return [p for p in parts if p['id'].startswith(prefix)]

def stage(items, order, dependencies=()):
    for p in items:
        p['order'] = order
        p['prerequisites'] = sorted({d['id'] for d in dependencies})

for cylinder in range(1, 4):
    tag = f'Cyl{cylinder:02d}'
    base = 200 + (cylinder - 1) * 10
    piston = match('201_Piston_' + tag)
    rod = match('202_Connecting_Rod_' + tag)
    pin = match('203_Piston_Wrist_Pin_' + tag)
    clips = match('205_Wrist_Pin_Circlip_' + tag)
    stage(piston, base + 1)
    stage(rod, base + 2, piston)
    stage(pin, base + 3, rod)
    stage(clips, base + 4, pin)

# Shaft clips are identified by CAD position, independently of the idler's bore clips.
inner = match('511_Idler_Shaft_Circlip_01') + match('511_Idler_Shaft_Circlip_04')
outer = match('511_Idler_Shaft_Circlip_02') + match('511_Idler_Shaft_Circlip_03')
shafts = match('509_Timing')
stage(inner, 515, shafts)
for group in recipe['subAssemblies']:
    if not group['rootPartId'].startswith('530_'):
        continue
    members = [p for p in parts if p.get('subAssemblyId') == group['id']]
    pulley = [p for p in members if 'Timing_Idler_Pulley' in p['id']]
    bearings = [p for p in members if 'Ball_Bearing' in p['id']]
    clips = [p for p in members if 'Bore_Circlip' in p['id']]
    start = 516 + 4 * (int(group['rootPartId'][-2:]) - 1)
    stage(pulley, start, inner)
    stage(bearings, start + 1, pulley)
    stage(clips, start + 2, bearings)
    stage(match(group['rootPartId']), 530, members + shafts + inner)
stage(outer, 531, match('530_'))
stage(match('512_Timing_Belt'), 532, match('530_') + inner + outer)

stage(match('790_Bolt_ISO_4762_M4'), 707, match('704_'))
stage(match('790_Bolt_ISO_4762_M8'), 710, match('705_'))

upper = match('901_Accessory_Upper')
lower = match('901_Accessory_Lower')
stage(upper, 901)
stage(lower, 902, upper)
stage(match('902_'), 903, upper + lower)
stage(match('903_') + match('904_'), 904, match('902_'))
stage(match('905_'), 905, match('903_'))
stage(match('906_'), 906, upper + lower)
inner_accessory = match('907_Idler_Shaft_Circlip_02')
stage(inner_accessory, 907, match('906_'))
stage(match('907_Accessory_Idler_Pulley'), 908, inner_accessory)
stage(match('907_Idler_Ball'), 909, match('907_Accessory_Idler_Pulley'))
stage(match('907_Idler_Bore'), 910, match('907_Idler_Ball'))
members = [p for p in parts if p.get('subAssemblyId') == '909_Accessory_Idler_Unit']
stage(match('909_Accessory_Idler_Unit'), 911, members + inner_accessory + match('906_'))
stage(match('907_Idler_Shaft_Circlip_01'), 912, match('909_Accessory_Idler_Unit'))
stage(match('908_Motor'), 913, upper + lower)
stage(match('990_Bolt'), 914, match('908_Motor'))
stage(match('990_Nut'), 915, match('990_Bolt'))
stage(match('910_Accessory_Drive_Belt'), 916, match('909_Accessory_Idler_Unit') + match('990_Nut'))

changed = {'chapter-2', 'chapter-5', 'chapter-7', 'chapter-9'}
# Drop obsolete chapter-local edges, retain external prerequisites and carrier members.
# Enforce each operation while allowing equal-priority fasteners in either order.
for chapter in changed:
    local = [p for p in parts if p['chapterId'] == chapter]
    local_ids = {p['id'] for p in local}
    previous = []
    for order in sorted({p['order'] for p in local}):
        peers = [p for p in local if p['order'] == order]
        for p in peers:
            retained = [d for d in p['prerequisites'] if d not in local_ids and d != removed]
            carrier_members = [m['id'] for m in local if m.get('subAssemblyId') == p['id']]
            p['prerequisites'] = sorted(set(retained + carrier_members + [q['id'] for q in previous]))
        previous = peers
for p in parts:
    p['prerequisites'] = [d for d in p['prerequisites'] if d != removed]
recipe['chapters'][4]['description'] = ('Fit camshaft hubs, inner washers and hub bolts before outer pulleys. '
    'Install both inner shaft circlips, bench-build and install the timing idlers, then fit outer shaft clips and belt.')
recipe['chapters'][8]['description'] = ('Fit the upper bracket before the lower bracket. Fit the idler shaft inner '
    'circlip, completed idler and outer circlip in sequence. Mount the motor and fit the drive belt.')
# Preserve valve-stack peer ordering and all authored geometry/fixture/tray poses.
recipe['parts'] = [p for chapter in recipe['chapters'] for p in
    (sorted((p for p in parts if p['chapterId'] == chapter['id']), key=lambda p: (p['order'], p['id']))
     if chapter['id'] in changed else [p for p in parts if p['chapterId'] == chapter['id']])]
by_id = {p['id']: p for p in parts}
assert len(by_id) == len(parts) == 325
assert all(d in by_id and by_id[d]['order'] <= p['order'] for p in parts for d in p['prerequisites'])
path.write_text(json.dumps(recipe, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(f'Updated retainer/accessory order: {len(parts)} items; motor wiring excluded.')
