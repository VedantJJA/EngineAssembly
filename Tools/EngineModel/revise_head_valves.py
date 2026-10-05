"""Revise only the cylinder-head bench sequence; retain every CAD component and pose."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / 'Assets/AssemblyRecipes/three-cylinder.json'
recipe = json.loads(path.read_text(encoding='utf-8-sig'))
parts = recipe['parts']
head = next(p for p in parts if p['id'] == '401_Cylinder_Head_01')
valves = sorted([p for p in parts if p['id'].startswith('404_')], key=lambda p:(p['id'].split('_Cyl')[1][:2], 'Exhaust' in p['id'], p['id']))
head.update(order=401, orientation=0, prerequisites=[])
for valve in valves:
    valve.update(order=402, orientation=2, prerequisites=[head['id']])

def nearest_valve(part):
    p = part['targetPosition']
    return min(valves, key=lambda v: sum((p[k]-v['targetPosition'][k])**2 for k in ('x', 'z')))

stacks = {v['id']: {'valve': v['id']} for v in valves}
for prefix, role in [('402_', 'seat'), ('403_', 'seal'), ('405_', 'spring'), ('406_', 'retainer'), ('407_', 'keepers')]:
    for part in (p for p in parts if p['id'].startswith(prefix)):
        valve = nearest_valve(part)
        stack = stacks[valve['id']]
        if role == 'keepers':
            stack.setdefault(role, []).append(part['id'])
        else:
            assert role not in stack, (role, part['id'], stack)
            stack[role] = part['id']
        part.update(order=403, orientation=1)

by_id = {p['id']: p for p in parts}
recommended = {}
roles = {'seat':'Spring seat', 'seal':'Stem seal', 'spring':'Valve spring', 'retainer':'Retainer'}
for index, stack in enumerate(stacks.values()):
    assert len(stack['keepers']) == 2, stack
    valve = by_id[stack['valve']]
    cylinder = int(valve['id'].split('_Cyl')[1][:2])
    number = int(valve['id'].rsplit('_',1)[1])
    label = 'C%d %s%d' % (cylinder, 'EX' if 'Exhaust' in valve['id'] else 'IN', number)
    valve['displayName'] = 'C%d %s valve %d' % (cylinder, 'exhaust' if 'Exhaust' in valve['id'] else 'intake', number)
    for role, before in [('seat','valve'), ('seal','seat'), ('spring','seal'), ('retainer','spring')]:
        by_id[stack[role]]['prerequisites'] = [stack[before]]
        by_id[stack[role]]['displayName'] = roles[role]+' · '+label
        recommended[stack[role]] = index*10+list(roles).index(role)
    for keeper in stack['keepers']:
        by_id[keeper]['prerequisites'] = [stack['retainer']]
        by_id[keeper]['displayName'] = 'Keeper half %s · %s' % ('AB'[stack['keepers'].index(keeper)], label)
        recommended[keeper] = index*10+4

recipe['chapters'][3]['description'] = ('Flip the loose head upside down and insert all 12 valves. Flip upright; build each stack: seat → seal → spring → retainer → two keepers. Install the complete head, then its bolts. F flips the active workpiece.')
parts.sort(key=lambda p: (recipe['chapters'].index(next(c for c in recipe['chapters'] if c['id']==p['chapterId'])), p['order'], recommended.get(p['id'],0), p['id']))
assert len(parts) == 325 and len(valves) == 12
path.write_text(json.dumps(recipe, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
(root/'ModelReview/valve-stacks.json').write_text(json.dumps(list(stacks.values()), indent=2)+'\n', encoding='utf-8')
print('Revised 12 individual valve stacks; all 84 valve-stack components and the head retained.')
