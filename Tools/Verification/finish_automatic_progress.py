"""Validate narrated automatic progression, then export phone and Quest players."""
import json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/automatic-release-status.json'
def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    state('aligning-new-voices')
    for script in ['verify_audio.py','align_narration.py']:
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration'/script)],cwd=ROOT,check=True)
    state('preparing')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Audio/Narration/English']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,report in [('AutomaticProgressVerification','automatic-progress-report.json'),('TeachingGuidanceVerification','teaching-guidance-report.json'),('QuestGripPlacementVerification','quest-grip-placement-report.json')]:
        state(method)
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/automatic-'+method+'.log'))],cwd=CLONE,check=True)
        result=json.loads((CLONE/'Tools/Verification'/report).read_text());shutil.copy2(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
    if '--test-only' not in sys.argv:
        state('building-phone-and-quest')
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
