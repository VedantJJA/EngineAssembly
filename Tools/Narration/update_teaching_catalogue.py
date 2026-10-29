"""Separate movement introductions from controls taught during assembly."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'Assets/Resources/WorkshopTutorial.json'
data=json.loads(path.read_text(encoding='utf-8'))
changes={
 'tutorial-pc-navigation': 'Walk with W, A, S and D, and look around with the mouse. Hold Shift to walk faster. Space jumps, and Control crouches. Try moving around the workshop before you begin assembling.',
 'tutorial-touch-navigation': 'Use the left joystick to walk around the garage. Drag on a clear area of the view to look around. The on-screen controls let you crouch, sprint and jump. Try moving around before you begin assembling.',
 'tutorial-vr-navigation': 'Move within your safe play area, or use the left stick to walk. Push the right stick left or right to turn in steps. Push it forward to aim a teleport arc, then release to travel to the marked clear floor. Practise moving to a comfortable position before you begin.',
 'tutorial-pc-placement': 'Carry the part and aim at a visible matching ghost. Click to fit it. Matching copies can fill any compatible unlocked socket. Turn or move around the workpiece until you can see the fitting surface. A ghost seen through solid metal cannot be selected through that metal.',
 'tutorial-ar-help': 'Open Settings to turn ghost hints and the direction arrow on or off. Next Step fits the next component for you. Captions explain the part and its function while narration plays. Chapters lets you revisit a section with earlier work already assembled.'
}
for lesson in data['lessons']:
    if lesson['id'] in changes: lesson['text']=changes[lesson['id']]
    if lesson['id']=='tutorial-pc-placement':lesson['controls']='Pick up a part first\nAim at a visible matching ghost + click   Install\nMatching copies can fill any unlocked matching socket'
    if lesson['id']=='tutorial-ar-help':lesson['controls']='SETTINGS   Ghost hints / direction arrow\nNEXT STEP   Install the next component\nCHAPTERS   Choose a section'
new=[dict(id='tutorial-mobile-navigation',platform='Mobile',title='Move your view',controls='One finger   Rotate the active build\nTwo fingers   Move across the screen\nPinch   Zoom',text='Drag with one finger to rotate the engine. Drag with two fingers to move it across the screen, and pinch to zoom. Only the active build moves. Use these gestures to find a comfortable view before assembling.'),
 dict(id='tutorial-pc-placement-advanced',platform='PC',title='Place and orient a part',controls='Q   Carry / placement mode\nT   Move / rotate\nHold right mouse + drag   Adjust the part',text='Press Q to switch from carrying to placement mode. Hold the right mouse button and move the mouse to adjust the part. T switches between moving and rotating. Medium aligns the rotation for you. Hard needs the correct position and orientation. Click the visible ghost when the part is aligned.')]
for lesson in new:
    if not any(item['id']==lesson['id'] for item in data['lessons']): data['lessons'].append(lesson)
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
