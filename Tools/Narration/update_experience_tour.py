import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/EngineOperationNarration.json'
data=json.loads(path.read_text())
descriptions={
'engine-operation-introduction':('How the engine works','An engine changes energy from burning fuel into a turning shaft. Before the engine moves, identify the main parts highlighted in blue.',[]),
'engine-stroke-intake':('1. Intake','Intake draws fresh air into the cylinder. The piston travels down and the intake valves open. Blue marks the incoming air. The exhaust valves remain closed.',['201_','404_Intake']),
'engine-stroke-compression':('2. Compression','Compression squeezes the trapped air. The piston travels up while the intake and exhaust valves stay closed. The shrinking space raises the air pressure and temperature. Gold marks the compressed air.',['201_']),
'engine-stroke-power':('3. Power','Burning fuel releases heat. Expanding hot gas pushes the piston down during the power stroke. The connecting rod carries the force to the crankshaft, making it turn. Orange marks the expanding gas.',['201_','202_','103_']),
'engine-stroke-exhaust':('4. Exhaust','Exhaust removes the used gas. The piston travels up while the exhaust valves open, pushing the gas out. Grey marks the outgoing gas. A complete four-stroke cycle takes two crankshaft turns. The camshafts turn once in that time.',['201_','404_Exhaust'])}
for cue in data['cues']:
    if cue['id'] in descriptions:
        cue['title'],cue['text'],cue['partPrefixes']=descriptions[cue['id']]
prelude=[
dict(id='engine-identify-pistons',title='Pistons and cylinders',text='Pistons are the moving crowns inside the three cylinders. Each piston slides up and down. The cylinder walls guide that movement, while rings around the piston help keep pressure in the space above it.',partPrefixes=['201_','205_','206_','207_']),
dict(id='engine-identify-rods',title='Connecting rods and crankshaft',text='Connecting rods join the pistons to the crankshaft below them. A rod can tilt at its joints as the piston moves. The offset arms of the crankshaft turn the up-and-down movement into rotation.',partPrefixes=['202_','203_','103_']),
dict(id='engine-identify-timing',title='Timing belt and camshafts',text='The timing belt links the crankshaft pulley to the two camshaft pulleys at the top. The camshafts turn at half the crankshaft speed. This keeps valve opening and closing matched to the piston positions.',partPrefixes=['512_','509_','502_','503_']),
dict(id='engine-identify-valves',title='Valves and springs',text='Intake valves admit fresh air. Exhaust valves let used gas leave. Raised lobes on the camshafts push the valve mechanism open. Valve springs close the valves again as each lobe turns away. Now follow one cylinder through intake, compression, power and exhaust.',partPrefixes=['404_','405_','406_','408_'])]
for cue in prelude:cue.update(startAngle=360,endAngle=360)
# Append new IDs after the existing voices so the locked voice seed of an old cue is stable.
old=[c for c in data['cues'] if not c['id'].startswith('engine-identify-')]
data['cues']=old+prelude
data['tourOrder']=['engine-operation-introduction']+[c['id'] for c in prelude]+[c['id'] for c in old if c['id']!='engine-operation-introduction']
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
