import json
from pathlib import Path
path=Path(__file__).resolve().parents[2]/'Assets/Resources/WorkshopTutorial.json';data=json.loads(path.read_text())
for lesson in data['lessons']:
    if lesson['id']=='tutorial-ar-place':
        lesson['controls']=lesson['controls'].replace('PLACE ENGINE','PLACE')
        lesson['text']='Allow camera access, then move the phone slowly over a clear, well-lit surface. Tap Place when a surface is found. The practice shapes appear at a comfortable working height. The engine uses the same workspace after practice. If AR is unavailable, choose phone view to continue without the camera.'
    elif lesson['id']=='tutorial-mobile-navigation':
        lesson['text']='Drag with one finger to rotate the active model. Drag with two fingers to move it across the screen, and pinch to zoom. Only the active build moves. Try these gestures with the practice shapes before starting the engine.'
path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
