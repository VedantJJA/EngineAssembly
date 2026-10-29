"""Validate flip guidance, narration expiry and Easy pickup, then export players."""
import json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/guidance-pickup-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    state('preparing-verification')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Shaders']:shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,report in [('GuidancePickupVerification','guidance-pickup-report.json'),('PracticePacingVerification','practice-pacing-report.json'),('QuestGripPlacementVerification','quest-grip-placement-report.json'),('StackCopiesVerification','stack-copies-report.json')]:
        state(method);started=time.time();process=subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/guidance-'+method+'.log'))],cwd=CLONE)
        output=CLONE/'Tools/Verification'/report
        if not output.exists() or output.stat().st_mtime<started:process.check_returncode();raise RuntimeError('No current report: '+report)
        result=json.loads(output.read_text());shutil.copy2(output,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
    for source in (ROOT/'Assets/Scripts').rglob('*.cs'):
        target=Path(str(source)+'.meta');generated=Path(str(CLONE/source.relative_to(ROOT))+'.meta')
        if not target.exists() and generated.exists():shutil.copy2(generated,target)
    if '--test-only' not in sys.argv:
        state('exporting-phone-and-quest');subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:state('failed',str(error));raise
