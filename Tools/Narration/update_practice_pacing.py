"""Short, action-gated practice lessons. Keep existing cue positions stable."""
import json
from pathlib import Path

path = Path(__file__).resolve().parents[2] / 'Assets/Resources/WorkshopTutorial.json'
data = json.loads(path.read_text(encoding='utf-8'))
lessons = {l['id']: l for l in data['lessons']}

def lesson(id, platform, order, title, controls, text, *actions, difficulty=''):
    entry = lessons.get(id)
    if entry is None:
        entry = {'id': id}
        data['lessons'].append(entry)
        lessons[id] = entry
    entry.update(platform=platform, practiceOrder=order, title=title, controls=controls,
                 text=text, difficulty=difficulty,
                 actions=[dict(id=a, target=n, label=label) for a, n, label in actions])

lesson('tutorial-pc-navigation','PC',0,'Walk and look','W A S D   Walk\nMouse   Look',
       'Practice with the shapes before building the engine. Walk a few steps with W, A, S and D. Move the mouse to look around. Take your time; the next instruction waits for you.',
       ('walk',1.2,'Walk a few steps'),('look',35,'Look around'))
lesson('tutorial-pc-stance','PC',1,'Sprint, jump and crouch','Shift + walk   Sprint\nSpace   Jump\nCtrl / C   Crouch',
       'Hold Shift while walking to sprint. Press Space to jump, then hold Control or C to crouch. Try each movement before continuing.',
       ('sprint',.6,'Sprint briefly'),('jump',1,'Jump'),('crouch',1,'Crouch'))
lesson('tutorial-pc-pickup','PC',2,'Pick up matching cubes','Aim + left click / E   Pick up one cube\nClick a second blue cube   Carry both',
       'Find the blue cubes on the nearby table. Aim at a cube and click to pick it up. Click a second cube to carry both. The inventory number shows how many matching parts you hold.',
       ('pickup',2,'Pick up two blue cubes'),('stack',1,'Carry matching cubes together'))
lesson('tutorial-pc-drop','PC',3,'Drop the cubes','G   Drop every held copy\nPick the cubes up again when you are ready',
       'Press G to drop the cubes. Both copies fall separately. Pick up a cube again for the placement practice.',
       ('drop',2,'Drop both cubes'),('repickup',1,'Pick up a dropped cube'))
lesson('tutorial-pc-placement','PC',5,'Fit a cube','Carry a cube\nAim at a visible matching ghost + click',
       'Find a cube-shaped ghost on the practice board. Carry a cube, aim at the visible ghost and click to fit it. In Easy difficulty the position and rotation are handled for you.',
       ('place',1,'Fit one cube'),difficulty='Easy')
lesson('tutorial-pc-placement-medium','PC',4,'Medium: move the held cube','Q   Carry / placement mode\nT   Move / rotate tool\nRight mouse + drag   Adjust the part',
       'Medium difficulty needs placement mode. Hold a cube and press Q to enter it. Press T to try the move tool, then hold the right mouse button and drag to move the cube. The final rotation aligns automatically.',
       ('placement-mode',1,'Enter placement mode with Q'),('placement-tool',1,'Switch the tool with T'),('manipulate-move',5,'Move the held cube'),difficulty='Medium')
lesson('tutorial-pc-placement-advanced','PC',4,'Hard: move and rotate','Q   Carry / placement mode\nT   Move / rotate tool\nRight mouse + drag   Adjust the part',
       'Hard difficulty needs the correct position and rotation. Hold a cube and press Q for placement mode. T switches between moving and rotating. Hold the right mouse button and drag to adjust the cube. Try both tools.',
       ('placement-mode',1,'Enter placement mode with Q'),('manipulate-rotate',5,'Rotate the held cube'),('manipulate-move',5,'Move the held cube'),difficulty='Hard')
lesson('tutorial-pc-fit-manual','PC',5,'Fit the aligned cube','Placement mode   Aim at its visible ghost + click\nQ   Return to carry mode if you need another cube',
       'Bring the cube close to a matching ghost on the practice board. Match its position and rotation, then aim at the visible target and click to fit it.',
       ('place',1,'Fit one cube'),difficulty='Hard')
lesson('tutorial-pc-fit-medium','PC',5,'Fit the positioned cube','Placement mode   Aim at its visible ghost + click\nQ   Return to carry mode if you need another cube',
       'Move the cube close to a matching ghost on the practice board. Aim at the visible target and click to fit it. Medium difficulty aligns the final rotation for you.',
       ('place',1,'Fit one cube'),difficulty='Medium')
lesson('tutorial-pc-lift','PC',6,'Adjust the lift height','Hold the red lever + drag up / down\nOr use Up / Down arrows',
       'Find the red lever beside the practice board. Hold the left mouse button on its red grip and drag up or down. Release to stop the lift. Try changing the board height now.',
       ('lift',.08,'Raise or lower the practice board'))
lesson('tutorial-pc-flip','PC',7,'Turn the practice board over','Red FLIP button / F   Turn the active lift\nLeft / Right arrows   Choose a lift',
       'Press the red Flip button beside the practice board, or press F. Wait while the hatch opens and the board turns below the floor. The same control turns an engine workpiece when a fitting step needs it.',
       ('flip',1,'Flip the practice board'))
lesson('tutorial-pc-help','PC',8,'Try the guides','H   Toggle ghost hints\nV   Toggle the direction arrow\nR   Replay this instruction',
       'Press H to hide the ghost hints, then press H again to restore them. Press V to try the direction arrow, then press V again to turn it off. R replays the current voice line whenever you need it.',
       ('hints',2,'Toggle hints off and back on'),('arrow',2,'Toggle the arrow on and back off'))
lesson('tutorial-pc-menus','PC',9,'Chapters, pause and settings','Tab   Chapters\nEsc   Pause / settings\nHold Alt   Use the guide buttons\nN   Auto-install next part',
       'Tab opens the chapter cards. Escape opens pause and settings, including volume, captions and difficulty. Open either menu and return to practice. N can fit the next part automatically if you get stuck.',
       ('menu',1,'Open a menu, then return'))

for platform,prefix in [('Touch','touch'),('VR','vr'),('Hands','hands')]:
    if platform=='Touch':
        nav=('Walk around the garage','Left joystick   Walk\nDrag clear view   Look',
             'Use the left joystick to walk a few steps. Drag on a clear part of the screen to look around. Practice with the shapes; each lesson waits until you have tried its control.', [('walk',1.2,'Walk a few steps'),('look',35,'Look around')])
        lesson('tutorial-touch-stance',platform,1,'Try movement buttons','Sprint   Move faster\nJump   Hop\nCrouch   Lower your view',
               'Try the Sprint button while moving, then tap Jump and Crouch. You can tap objects directly to interact; you do not have to aim a crosshair.',('sprint',.6,'Sprint briefly'),('jump',1,'Jump'),('crouch',1,'Crouch'))
        parts=('Carry a practice cube','Tap a cube   Pick up\nTap its visible ghost   Install',
               'Tap a blue cube on the nearby table to pick it up. Tap a visible cube-shaped ghost on the practice board to fit it. Matching parts can be collected one at a time into the same inventory stack.')
        lifttext='Hold the red lift lever and drag up or down to change the practice board height. Release to stop. Try moving the lift now.'
        helptext='Open Pause or Chapters, then return to practice. Settings contains volume, captions, difficulty and guide toggles. Next Step can install a part automatically when you need help.'
    elif platform=='VR':
        nav=('Quest 2: walk and turn','Left Touch stick   Walk\nRight Touch stick left / right   Turn',
             'On Meta Quest 2, use the left Touch-controller thumbstick to walk a few steps. Push the right thumbstick sideways to turn. Keep inside your safe play area.', [('walk',.7,'Move a few steps'),('look',25,'Look or turn')])
        lesson('tutorial-vr-teleport',platform,1,'Quest 2: teleport','Right stick forward   Aim at clear floor\nRelease the stick   Travel',
               'Push the right Touch-controller thumbstick forward to aim a teleport arc at clear floor. Release the stick when the landing marker is valid. Try one teleport now.',('teleport',1,'Teleport to clear floor'))
        parts=('Quest 2: fit a cube','Side grip   Hold a cube\nNear visible ghost   Preview\nRelease side grip   Install\nPoint + trigger   Alternative placement',
               'Point at a blue cube and hold the side grip button to carry it. Move the cube near a visible matching ghost to preview its fit. Move away to cancel. Release the side grip at the ghost to install it. Pointing at the ghost and pressing the trigger also places a held cube.')
        lifttext='Reach for the red lever with a Touch controller. Hold the side grip and move your controller up or down to adjust the practice board height. Release the grip to stop. Try moving the lift now.'
        helptext='Press the Menu button with three lines on the left Touch controller. Open settings or chapters, then return to practice. A on the right controller or X on the left installs the next part automatically.'
    else:
        nav=('Quest 2: look and move','Walk inside your safe play area\nKeep hands in view',
             'Keep your hands where the Quest 2 cameras can see them. Look around, then walk a little inside your safe play area. The practice shapes let you try the controls before building the engine.', [('walk',.35,'Move safely in your play area'),('look',25,'Look around')])
        lesson('tutorial-hands-teleport',platform,1,'Travel with your wrist controls','Wrist TELEPORT   Point and pinch\nPoint at clear floor + pinch   Travel',
               'Point at Teleport on your left wrist panel and pinch. Point your right hand at clear floor and pinch again when the landing marker is valid. Try one teleport now.',('teleport',1,'Teleport to clear floor'))
        parts=('Take and fit a cube','Near cube   Pinch or close hand\nNear visible ghost   Preview\nOpen hand   Install',
               'Reach for a blue cube. Pinch or close your hand to take it, and keep that gesture while moving. Bring the cube near a visible matching ghost. Move away to cancel the preview. Open your hand at the ghost to install it.')
        lifttext='Reach for the red lift lever. Pinch or close your hand around it, then move your hand up or down to adjust the practice board height. Open your hand to stop. Try moving the lift now.'
        helptext='Point at Menu on your left wrist panel and pinch. Open settings or chapters, then return to practice. Next on the wrist panel installs the next part automatically when you need help.'
    title,controls,text,actions=nav
    lesson(f'tutorial-{prefix}-navigation',platform,0,title,controls,text,*actions)
    lesson(f'tutorial-{prefix}-parts',platform,3,*parts,('pickup',1,'Take a blue cube'),('place',1,'Fit one cube'))
    lesson(f'tutorial-{prefix}-lift',platform,6,'Adjust the lift height','Hold red lever   Move up / down\nRelease   Stop',lifttext,('lift',.08,'Raise or lower the board'))
    lesson(f'tutorial-{prefix}-flip',platform,7,'Turn the board over','Touch FLIP / point and select   Turn over',
           'Press the red Flip button beside the practice board. Wait for the hatch to open and the board to turn, then come back up. The same control turns an engine workpiece when needed.',('flip',1,'Flip the practice board'))
    lesson(f'tutorial-{prefix}-help',platform,9,'Try the menus','Menu / Pause   Chapters and settings\nNext   Automatic placement',helptext,('menu',1,'Open a menu, then return'))

for platform,prefix in [('Mobile','mobile'),('AR','ar')]:
    lesson(f'tutorial-{prefix}-navigation' if platform=='Mobile' else 'tutorial-ar-gestures',platform,1,'Turn the practice board','One finger   Rotate',
           'Drag with one finger to turn the practice board. Try rotating it until you can see the cube-shaped targets. Only the active build moves.',('rotate-build',10,'Rotate the practice board'))
    lesson(f'tutorial-{prefix}-pan',platform,2,'Move and zoom','Two fingers   Move across view\nPinch   Zoom',
           'Drag with two fingers to move the practice board across the view. Pinch your fingers together or apart to zoom. Try both gestures before continuing.',('pan-build',10,'Move the board with two fingers'),('zoom-build',.07,'Pinch to zoom'))
lesson('tutorial-ar-place','AR',0,'Choose your workspace','Scan a clear, well-lit surface\nTap PLACE when ready',
       'Allow camera access and scan a clear, well-lit surface. Tap Place when a surface is found. Set the practice shapes at a comfortable working height. The engine will use this workspace after practice.',('workspace',1,'Place your workspace'))
lesson('tutorial-ar-assemble','AR',3,'Choose a cube, then fit it','Bottom cards   Choose a blue cube\nTap its visible ghost   Install',
       'Choose a blue cube from the cards at the bottom. Tap a visible cube-shaped target on the practice board to fit it. A target hidden behind solid material must be viewed from another angle.',('pickup',1,'Select a blue cube card'),('place',1,'Fit one cube'))
lesson('tutorial-ar-help','AR',9,'Try the help settings','SETTINGS   Volume / captions / hints / arrow\nNEXT STEP   Automatic placement',
       'Open Settings to find volume, captions, ghost hints and the direction arrow. Close settings to return to practice. Next Step fits the next component for you when you need help.',('menu',1,'Open settings, then return'))

for platform in ['PC','Touch','VR','Hands','Mobile','AR']:
    lesson('tutorial-'+platform.lower()+'-finish',platform,10,'Finish the practice board','Fit the remaining cubes, gold cylinder and purple ball',
           'Fit the remaining shapes into their matching targets on the practice board. Take as long as you need. The engine chapter starts after the board is complete.',('place',5,'Fit all five practice shapes'))
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
