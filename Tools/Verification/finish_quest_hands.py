import json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/quest-hands-release-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    state('preparing')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Art/VRHands']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for name in ['manifest.json','packages-lock.json']:
        source=ROOT/'Packages'/name;target=CLONE/'Packages'/name
        if source.read_bytes()!=target.read_bytes():shutil.copy2(source,target)
    for method,report in [('QuestHandsVerification','quest-hands-report.json'),('QuestGripPlacementVerification','quest-grip-placement-report.json'),('QuestLiftVerification','quest-lift-report.json')]:
        state(method)
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/hands-'+method+'.log'))],cwd=CLONE,check=True)
        result=json.loads((CLONE/'Tools/Verification'/report).read_text());shutil.copy2(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
    if '--test-only' not in sys.argv:
        state('waiting-for-new-hand-voices')
        while json.loads((ROOT/'Tools/Narration/progress.json').read_text())['status'] not in ['complete','failed']:time.sleep(3)
        if json.loads((ROOT/'Tools/Narration/progress.json').read_text())['status']!='complete':raise RuntimeError('Hand narration generation failed')
        state('building-phone-and-quest')
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
