"""Name Quest 2 Touch controls explicitly in the spoken control lessons."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/WorkshopTutorial.json'
data=json.loads(path.read_text(encoding='utf-8'))
updates={
 'tutorial-vr-navigation':('Meta Quest 2 movement','Left Touch thumbstick   Walk\nRight thumbstick left / right   Turn\nRight thumbstick forward, then release   Teleport','On Meta Quest 2, use the left Touch-controller thumbstick to walk. Move the right thumbstick left or right to turn. Push the right thumbstick forward to aim a teleport arc, then release it to travel. You can also walk inside your safe play area.'),
 'tutorial-vr-parts':('Quest 2: carry and fit','Side grip / middle finger   Hold to carry\nNear a visible ghost   Preview    Move away   Undo\nRelease grip at the ghost   Install\nIndex-finger trigger   Point and select','On Quest 2, point at a loose part and hold the side grip button with your middle finger to carry it. Bring it near a matching visible ghost to preview the fit. Move your hand away to undo the preview. Release the grip while seated to install. You can also point at the visible ghost and press the index-finger trigger to place it.'),
 'tutorial-vr-lift':('Quest 2: lift controls','Near the red lever + side grip   Hold\nMove your Touch controller up / down   Lift\nRelease grip   Stop\nTouch FLIP / point + index trigger   Turn over','Reach for the red lift lever with your Quest 2 Touch controller. Hold the side grip button and move the controller up or down to raise or lower the assembly. Release the grip to stop. Touch the red flip button, or point at it and press the index-finger trigger, to turn the assembly over.'),
 'tutorial-vr-help':('Quest 2: help and menus','A on right / X on left   Install next part\nLeft Menu button, three lines   Pause / chapters\nPoint + index-finger trigger   Choose a card','On Quest 2, press A on the right Touch controller or X on the left to install the next component automatically. Press the Menu button with three lines on the left controller to open pause, chapters and settings. Point at a menu card and press the index-finger trigger to select it. Control cards close automatically after their narration.')
}
for lesson in data['lessons']:
    if lesson['id'] in updates:lesson['title'],lesson['controls'],lesson['text']=updates[lesson['id']]
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
