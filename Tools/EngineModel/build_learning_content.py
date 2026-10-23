"""Author game captions, part cards and a plain narration script from the CAD recipe."""
import json
import re
from pathlib import Path
from collections import OrderedDict
from beginner_instructions import CAPTIONS, EXPLANATIONS

root = Path(__file__).resolve().parents[2]
# Preserve the reviewed split cue catalogue; the legacy grouping below predates
# distinct fastener sizes and the beginner lessons.
existing=root/'Assets/Resources/EngineLearning.json'
if existing.exists() and json.loads(existing.read_text(encoding='utf-8-sig')).get('version',0)>=3:
    from rewrite_learning_lessons import main
    main()
    raise SystemExit(0)
recipe = json.loads((root/'Assets/AssemblyRecipes/three-cylinder.json').read_text(encoding='utf-8-sig'))
parts = recipe['parts']
by_id = {p['id']: p for p in parts}
# These instructions describe the virtual model. They do not invent torque or workshop specifications.
directions = {
101: 'Place the cylinder block on the workpiece. The three cylinder bores identify the top; the crankshaft opening is underneath.',
102: 'Turn the block upside down. Fit the two needle bearings and four upper main-bearing shells into their matching crankshaft bearing positions.',
103: 'Lower the crankshaft into the bearings in the open crankcase. Align its journals with the bearing supports.',
201: 'Start the cylinder 1 piston on its bench fixture. Cylinder 1 is the bore at the timing-drive end. Place the piston with its crown above the wrist-pin bore.',
202: 'Put the small end of the connecting rod between the piston bosses. Align its bore with the wrist-pin holes.',
203: 'Slide the wrist pin through the piston and the connecting rod small end.',
204: 'Fit both wrist-pin circlips into the grooves at opposite ends of the seated wrist pin.',
205: 'Check that both wrist-pin circlips are seated before continuing.',
206: 'Fit both big-end studs into the connecting rod. Leave the separate rod cap off for now.',
207: 'Fit the oil-control ring into the lowest ring groove on the piston.',
208: 'Fit the second compression ring into the middle piston groove.',
209: 'Fit the top compression ring into the highest piston groove.',
230: 'Keep the engine upright. Install the complete piston units into their bores: C1 at the timing end, C2 in the middle, C3 at the flywheel end. Each open rod big end goes around its crankshaft journal.',
240: 'Turn the engine upside down. Fit each separate big-end cap around the lower half of its crankshaft journal, against the matching rod.',
241: 'Fit the two nuts on each connecting-rod cap stud. Complete all three rod caps.',
301: 'Keep the engine upside down. Fit the four lower main-bearing shells into the lower bearing positions.',
302: 'Place the lower crankcase bedplate over the bearing supports on the underside of the engine.',
303: 'Install the bedplate bolts in their matching holes around the lower crankcase.',
304: 'Start the oil sump on the separate bench fixture, with its open bowl facing upward.',
305: 'Fit the oil-sump baffle inside the open sump. Align its four screw holes with the mounting points.',
306: 'Install the four screws securing the baffle inside the sump.',
307: 'Keep the engine upside down. Pick up the complete sump-and-baffle unit and place it against the underside of the lower crankcase.',
308: 'Fit the oil-sump fasteners into the matching holes around the installed sump.',
309: 'Turn the engine upright. Fit the flywheel onto the rear flange of the crankshaft.',
310: 'Place the flywheel retaining plate over the centre of the flywheel.',
311: 'Install the six bolts through the flywheel retaining plate.',
401: 'Place the loose head on its bench. C1 is the timing-end chamber, C2 the middle, C3 the flywheel end. Within each valve pair, valve 1 is nearer the flywheel end; valve 2 is nearer the timing end.',
402: 'Flip the loose head upside down. Insert all twelve valves from the combustion-chamber side, guiding each stem through its matching guide. There are two intake and two exhaust valves per cylinder.',
480: 'Turn the engine upright. Pick up the complete cylinder-head unit from the bench and align it with the top face of the three-cylinder block.',
490: 'Install all eight cylinder-head bolts in the matching holes through the head.',
501: 'Fit the eight camshaft needle bearings into the supports along the top of the head.',
502: 'Fit the intake and exhaust camshafts into their respective bearing supports on the head.',
503: 'Place the front engine cover on the timing-drive end of the engine.',
504: 'Insert the two pulley keys into the keyways at the ends of the camshafts.',
505: 'Slide each camshaft pulley hub onto its keyed camshaft end.',
506: 'Place each camshaft washer inside its pulley hub before fitting the hub bolt or outer timing pulley.',
507: 'Install the two bolts through the seated washers to secure the camshaft pulley hubs.',
508: 'Insert the crankshaft pulley key into the crankshaft end keyway.',
509: 'Fit the outer camshaft timing pulleys onto the secured hubs, and the crankshaft pulley onto its keyed shaft.',
510: 'Place the crankshaft pulley washer over the crankshaft end, against the pulley.',
511: 'Install the outer pulley fasteners in their matching holes and on the crankshaft end.',
512: 'Fit the timing-idler mounting plate beside the timing pulleys.',
513: 'Install the four screws securing the timing-idler mounting plate.',
514: 'Fit the two timing-idler shafts into the mounting plate.',
515: 'Fit the two inner shaft circlips, one on each timing-idler shaft, in the grooves nearest the mounting plate.',
516: 'Start timing idler 1 on the bench. Place its pulley before inserting the bearings.',
517: 'Fit both ball bearings into the bore of timing idler 1.',
518: 'Fit both bore circlips into timing idler 1 to retain its bearings.',
520: 'Start timing idler 2 on the bench. Place its pulley before inserting the bearings.',
521: 'Fit both ball bearings into the bore of timing idler 2.',
522: 'Fit both bore circlips into timing idler 2 to retain its bearings.',
530: 'Slide both completed timing-idler units onto their matching shafts, against the installed inner shaft circlips.',
531: 'Fit the two outer shaft circlips into the exposed grooves to retain the installed timing idlers.',
532: 'Route the timing belt around the crankshaft pulley, both camshaft pulleys and the timing idlers.',
601: 'Place the cylinder-head cover over the camshafts on top of the head.',
602: 'Install the cylinder-head cover bolts around the cover.',
603: 'Fit the three fuel injectors into their matching cylinder-head positions.',
604: 'Place the fuel rail over the tops of the three injectors.',
605: 'Install the two screws securing the fuel rail.',
606: 'Connect the fuel return line to its matching fuel-system connection.',
607: 'Place the crankcase breather housing against its mounting face.',
608: 'Install the two screws securing the breather housing.',
609: 'Connect the breather hose between its matching ports.',
701: 'Fit the combined exhaust manifold and turbocharger housing to the exhaust side of the cylinder head.',
702: 'Install the four screws securing the exhaust-manifold and turbocharger housing.',
703: 'Fit the two turbo-shaft bearings into the turbocharger housing.',
704: 'Slide the turbo rotor shaft through the two bearing positions.',
705: 'Fit the two circlips into the retaining grooves on the turbo shaft.',
706: 'Fit the compressor wheel and turbine wheel onto their respective ends of the turbo shaft.',
707: 'Install the two M4 turbo-wheel fasteners in their matching positions.',
708: 'Fit the exhaust outlet and catalyst housing to the turbine outlet.',
709: 'Place the band clamp around the exhaust-outlet joint.',
710: 'Install the M8 bolt after the turbo outlet clamp is seated.',
801: 'Fit the intake manifold to the intake side of the cylinder head.',
802: 'Connect the intake pipe fitting and union to their matching intake connections.',
803: 'Connect the external service tube along its matching route on the engine.',
901: 'Fit the upper accessory mounting bracket to the engine first.',
902: 'Fit the lower accessory mounting bracket below the upper bracket.',
903: 'Place accessory spacers A and B into their matching bracket positions.',
904: 'Mount the alternator and starter motor onto their matching supports.',
905: 'Fit the alternator pulley onto the alternator shaft.',
906: 'Fit the accessory-idler shaft into its mounting support.',
907: 'Fit the inner shaft circlip in the groove nearest the accessory-idler mounting support.',
908: 'Start the accessory idler on the bench. Place its pulley before inserting the bearings.',
909: 'Fit both ball bearings into the accessory-idler pulley bore.',
910: 'Fit the two bore circlips into the accessory idler to retain its bearings.',
911: 'Slide the complete accessory-idler unit onto the shaft, against the installed inner shaft circlip.',
912: 'Fit the outer shaft circlip into the exposed groove to retain the accessory idler.',
913: 'Mount the complete motor unit onto its supports. Its internal components remain one assembly item.',
914: 'Install both motor mounting bolts through the matching supports.',
915: 'Fit both motor mounting nuts onto the installed bolts.',
916: 'Route the accessory drive belt around its matching pulleys to complete the engine.',
}
directions.update(CAPTIONS)
for cyl in (2,3):
    for offset in range(9):
        directions[201+(cyl-1)*10+offset] = directions[201+offset].replace('piston 1', f'piston {cyl}').replace('cylinder 1', f'cylinder {cyl}').replace('Cylinder 1', f'Cylinder {cyl}')
directions[211]=directions[211].replace('the timing-drive end','the centre of the block')
directions[221]=directions[221].replace('the timing-drive end','the flywheel end')

steps=[]
for chapter in recipe['chapters']:
    groups=OrderedDict()
    for p in parts:
        if p['chapterId']==chapter['id']:
            if p['order']==403:
                # A stack-local dependency chain must never become one global family instruction.
                key=('keepers',p['prerequisites'][0]) if p['id'].startswith('407_') else p['id']
            else: key=p['order']
            groups.setdefault(key,[]).append(p)
    for key,group in groups.items():
        p=group[0]
        voice_id=''
        if p['order']==403:
            roles={
                '402': 'Fit the spring seat, the thin ring, around the raised valve-guide tube.',
                '403': 'Slide the small stem seal over the valve stem. It rests inside the spring seat.',
                '405': 'Slide the spring over the valve stem. Its lower end rests on the spring seat.',
                '406': 'Place the cap-shaped retainer on top of the spring. The keepers will lock it to the stem.',
                '407': 'Fit both tiny keeper halves into the groove near the top of the valve stem, on opposite sides. They lock the retainer in place.',
            }
            text=roles[p['id'][:3]]
            voice_id='valve-'+p['id'][:3]
        else:
            assert p['order'] in directions, p['order']
            text=directions[p['order']]
        extra=EXPLANATIONS.get(p['order'],'')
        if p['order']==403:
            extra=''
        if 201<=p['order']<=229:
            base_order=201+(p['order']-201)%10
            voice_id='piston-'+str(base_order)
            extra=EXPLANATIONS.get(base_order,'')
        narration=(text+' '+extra).strip()
        for short,spoken in [('C1','cylinder one'),('C2','cylinder two'),('C3','cylinder three')]:narration=narration.replace(short,spoken)
        steps.append(dict(id=f"{chapter['id']}-cue-{len(steps)+1:03}", chapterId=chapter['id'], partIds=[x['id'] for x in group], caption=text, narration=narration, voiceId=voice_id))

families=[
('Complete_Oil_Sump','A bench-built oil sump containing its baffle plate and four baffle screws, installed as one unit beneath the crankcase.'),
('Timing_Idler_Mounting_Plate','Supports the two timing-idler shafts beside the timing pulleys.'),
('Complete_Cylinder_Head','A bench-built cylinder head containing all twelve valves and their spring stacks.'),
('Flywheel_Retaining_Plate','Secures the flywheel at its crankshaft flange when fitted with its mounting bolts.'),
('Pulley_Key','Locates a pulley or hub rotationally on a shaft keyway.'),
('Pulley_Washer','Distributes fastener contact load at the pulley seating face.'),
('Pulley_Hub','Connects a pulley to the keyed shaft end.'),
('Timing_Pulley','Transfers belt rotation between the crankshaft and camshafts.'),
('Crankshaft_Pulley','Drives its belt from the crankshaft end.'),
('Cylinder_Block','The main engine structure. Its bores guide the pistons and its crankcase supports the crankshaft.'),
('Cylinder_Head_Cover','Closes the top of the cylinder head and encloses the camshafts.'),
('Cylinder_Head','Forms the top of the combustion chambers and carries the valves, springs and camshafts.'),
('Complete_Cylinder_Head','A bench-built cylinder head containing all twelve valves and their spring stacks.'),
('Piston_Rod_Unit','A complete bench-built piston, connecting rod, pin, circlips and rings, installed as one unit.'),
('Piston_Wrist_Pin','Connects the piston to the connecting rod while allowing the rod to pivot.'),
('Wrist_Pin_Circlip','Retains the wrist pin in the piston bore.'),
('Big_End_Cap','The separate lower half of the connecting-rod big end, fitted around the crankshaft journal.'),
('Connecting_Rod','Transfers force between the piston and the crankshaft.'),
('Oil_Control_Ring','Controls the oil film on the cylinder wall.'),
('Compression_Ring','Helps seal combustion pressure between the piston and cylinder wall.'),
('Piston','Moves in a cylinder bore, receiving combustion force and transmitting it to the connecting rod.'),
('Valve_Spring_Seat','Supports the bottom of the valve spring around its guide.'),
('Valve_Stem_Seal','Controls oil passing along the valve stem into the port.'),
('Valve_Spring_Retainer','Supports the top of the valve spring and is retained by the split keepers.'),
('Valve_Keeper','One of a pair of split retainers that locks the spring retainer to the valve stem.'),
('Valve_Spring','Returns the valve toward its closed position.'),
('Intake_Valve','Opens the intake passage into its combustion chamber.'),
('Exhaust_Valve','Opens the exhaust passage out of its combustion chamber.'),
('Bearing','Supports a rotating shaft or pulley and reduces friction. Internal bearing elements belong to this single assembly item.'),
('Camshaft','Uses its lobes to operate the cylinder-head valves.'),
('Crankshaft','Converts the pistons and connecting rods reciprocating motion into rotation.'),
('Bedplate','Supports the lower main-bearing positions and closes the lower crankcase structure.'),
('Baffle','Restricts oil movement within the sump region.'),
('Oil_Sump','Collects oil beneath the crankcase.'),
('Flywheel','Stores rotational energy and smooths crankshaft rotation.'),
('Timing_Belt','Connects the timing pulleys to coordinate crankshaft and camshaft rotation.'),
('Drive_Belt','Transfers rotation between the accessory pulleys.'),
('Idler_Unit','A pulley assembled with its bearings and bore circlips, then installed onto a shaft as one unit.'),
('Pulley','Guides a belt or transfers rotation through a belt drive.'),
('Circlip','Fits into a groove to retain a shaft, bearing or pulley axially.'),
('Fuel_Injector','Delivers fuel to its cylinder.'),
('Fuel_Rail','Supplies the injectors through a shared distribution rail.'),
('Fuel_Return','Carries returning fuel from the fuel-system connection.'),
('Breather','Provides a route for crankcase ventilation.'),
('Compressor_Wheel','Compresses intake air on the compressor side of the turbocharger.'),
('Turbine_Wheel','Receives energy from exhaust flow to drive the turbocharger shaft.'),
('Turbo_Rotor_Shaft','Connects the turbocharger turbine and compressor wheels.'),
('Turbocharger_Housing','Contains the turbocharger and routes exhaust from the cylinder head.'),
('Catalyst','Routes exhaust away from the turbocharger through the catalyst housing.'),
('Intake_Manifold','Distributes intake air to the cylinder-head intake ports.'),
('Alternator','Converts mechanical rotation into electrical energy.'),
('Starter_Motor','Cranks the engine during starting.'),
('Motor_Unit','A complete motor assembly; internal meshes are treated as one assembly item.'),
('Wiring','The modelled electrical connection route beside the motor unit.'),
('Key','Locates a hub or pulley rotationally on a shaft keyway.'),
('Washer','Distributes fastener contact load at its seating face.'),
('Stud','A threaded attachment post used with a separate nut.'),
('Bolt','A threaded fastener securing the adjoining components.'),
('Screw','A threaded fastener securing the adjoining components.'),
('Nut','A threaded fastener installed onto a bolt or stud.'),
('Bracket','Supports an accessory in its mounting position.'),
('Spacer','Maintains the modelled spacing between the mounted components.'),
('Clamp','Secures the joint between adjoining exhaust components.'),
('Front_Engine_Cover','Closes the timing end of the engine.'),
('Shaft','Supports and locates a rotating component.'),
('Pipe','Connects the adjoining ports along the modelled fluid or air route.'),
('Tube','Connects the adjoining ports along the modelled service route.'),
]
details=[]
for p in parts:
    description=next((text for token,text in families if token in p['id']), 'A modelled engine component fitted at the position shown in the assembly sequence.')
    details.append(dict(partId=p['id'], title=p['displayName'], description=description))
payload=dict(version=2, recipeTitle=recipe['title'], steps=steps, details=details)
output=root/'Assets/Resources/EngineLearning.json'
output.write_text(json.dumps(payload,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
script=['Welcome to Engine Lab. You will build a three-cylinder engine one section at a time. Each instruction first names the part, then tells you where it fits. Read the part label before picking it up. Complete the current group before taking parts for the next group.',
'The timing-belt end is the end with the belt pulleys. The flywheel end is the opposite end, where the large round flywheel disc fits. Cylinder one is nearest the timing-belt end, cylinder two is in the middle, and cylinder three is nearest the flywheel end.',
'Some sections are built separately. These are called subassemblies: a piston and connecting rod, for example. Finish the separate unit, then move the whole unit into the engine. You can turn or raise a workpiece for a better view without changing its assembly order.',
'On the computer, use Left and Right Arrow to choose a lift, and Up and Down Arrow to adjust its height. The white lift is selected. F lowers that workpiece below the floor, turns it, and brings it back. V shows a pointer to the next part or its fitting position. On a phone, choose the next part card and tap its visible target. Next Step demonstrates one installation for you.',
'This is a learning sequence for the virtual model. No tightening torque, timing setting, or real workshop adjustment is specified.','']
for chapter in recipe['chapters']:
    script += [chapter['title'].upper(),'']
    for index,cue in enumerate(s for s in steps if s['chapterId']==chapter['id']):
        script += [f'Step {index+1}. '+cue['narration'],'']
(root/'Docs/AssemblyNarration.txt').write_text('\n'.join(script),encoding='utf-8')
repair_source=(root/'Assets/Scripts/Assembly/PCRepairChapter.cs').read_text(encoding='utf-8-sig')
repair_lines=re.findall(r'new Step\([^,]+,(?:true|false),"([^"]+)"\)',repair_source)
assert len(repair_lines)==14
(root/'Docs/RepairNarration.txt').write_text('TIMING IDLER BEARING REPLACEMENT\n\nReplace one faulty bearing while keeping the reusable parts. A circlip is a small split retaining ring that fits in a groove. The shaft rings keep the pulley on its support; the bore rings keep the bearings inside the pulley. Follow the labelled parts in this order.\n\n'+'\n\n'.join(f'Step {i+1}. {line}' for i,line in enumerate(repair_lines))+'\n',encoding='utf-8')
assert len([id for s in steps for id in s['partIds']])==len(parts)
print(f'Authored {len(steps)} cues covering all {len(parts)} recipe items.')
