import json
from pathlib import Path
root=Path(__file__).resolve().parents[2];path=root/'Assets/Resources/WorkshopTutorial.json';data=json.loads(path.read_text())
lessons=[
('navigation','Quest 2: use your hands','Walk in your safe play area\nWrist TELEPORT   Point and pinch\nWrist MENU   Pause / settings',
'In Quest 2 hand mode, keep your hands in view of the headset. Walk inside your safe play area to move around. To travel farther, point at TELEPORT on your left wrist panel and pinch. Point your right hand at clear floor and pinch again to travel. Point at MENU on the wrist panel and pinch to open the game menu.'),
('parts','Take and fit a part','Near a part   Pinch or close your hand\nHold the gesture   Carry\nNear a visible ghost   Preview\nOpen your hand   Install',
'Reach close to a loose component. Pinch your thumb and index finger, or close your hand, to take it. Keep holding the gesture as you move the component. Bring it near a matching visible ghost to preview the fit. Move away to cancel the preview. Open your hand while the component is fitted in the ghost to install it.'),
('lift','Move and turn the assembly','Near the red lever   Pinch or grip\nMove your hand up / down   Lift\nOpen your hand   Stop\nTouch the red FLIP button   Turn over',
'Reach for the red lift lever. Pinch or close your hand around it, then move your hand up or down to adjust the assembly height. Open your hand to stop the lift. Touch the red flip button to turn the assembly over. Keep your hands visible to the headset while operating the controls.'),
('help','Wrist controls','Point at a card + pinch   Select\nWrist MENU   Chapters / settings\nWrist NEXT   Install next component',
'Point at a menu card and pinch your thumb and index finger to select it. Use MENU on your left wrist panel for chapters and settings. Use NEXT on the wrist panel to install the next component automatically. Control cards close after their narration finishes.')]
for suffix,title,controls,text in lessons:
    item=dict(id='tutorial-hands-'+suffix,platform='Hands',title=title,controls=controls,text=text)
    found=next((i for i,x in enumerate(data['lessons']) if x['id']==item['id']),None)
    if found is None:data['lessons'].append(item)
    else:data['lessons'][found]=item
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
