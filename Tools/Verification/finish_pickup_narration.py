"""Validate pickup-driven speech replacement and export phone and Quest players."""
import json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/pickup-narration-status.json'
def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    state('preparing')
    shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    for method,report in [('ContextDisplayVerification','context-display-report.json'),('StackCopiesVerification','stack-copies-report.json'),('QuestGripPlacementVerification','quest-grip-placement-report.json'),('PracticePacingVerification','practice-pacing-report.json')]:
        state(method);started=time.time()
        result=subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/pickup-'+method+'.log'))],cwd=CLONE)
        source=CLONE/'Tools/Verification'/report
        assert source.exists() and source.stat().st_mtime>=started,'No current report: '+report
        data=json.loads(source.read_text());shutil.copy2(source,ROOT/'Tools/Verification'/report)
        assert data['passed'],data.get('error',report)
    if '--test-only' not in sys.argv:
        state('exporting-phone-and-quest')
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
