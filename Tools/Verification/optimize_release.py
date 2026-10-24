"""Compare hot paths, verify interaction regressions, then export both release APKs."""
import json, shutil, subprocess, sys, time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/performance-release-status.json'

def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

try:
    state('preparing')
    for folder in ['Assets/Scripts','Assets/Settings','Assets/Resources']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,report in [
        ('EnginePerformanceVerification.After','performance-after.json'),
        ('ValvePlacementVerification.RunBatch','valve-placement-report.json'),
        ('QuestLiftVerification.RunBatch','quest-lift-report.json')]:
        state(method)
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method,'-logFile',str(ROOT/('Tools/Verification/optimized-'+method.split('.')[0]+'.log'))],cwd=CLONE,check=True)
        result=json.loads((CLONE/'Tools/Verification'/report).read_text())
        shutil.copy2(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error','Verification failed'))
    state('full-regressions-and-release-builds')
    subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finish_display_update.py'),'--repair'],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
