"""Verify physical stack copies and interchangeable sockets, then rebuild both APKs."""
import json
import ctypes
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/('Tools/Verification/stack-floor-status.json' if '--only-stack' in sys.argv else 'Tools/Verification/stack-release-status.json')

def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

try:
    if '--wait' in sys.argv:
        state('waiting-for-release-builds')
        process_id=int(sys.argv[sys.argv.index('--wait')+1]);kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
        handle=kernel.OpenProcess(0x100000,False,process_id)
        if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
        release=json.loads((ROOT/'Tools/Verification/stack-release-status.json').read_text())
        if release['stage']!='complete':raise RuntimeError('Release builds did not finish successfully')
    state('preparing')
    shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    for method,report in [
        ('StackCopiesVerification','stack-copies-report.json'),
        ('PlayerCollisionInventoryVerification','player-collision-inventory-report.json'),
        ('WorkshopControlsVerification','workshop-controls-report.json'),
        ('QuestGripPlacementVerification','quest-grip-placement-report.json'),
        ('ExperienceRefreshVerification','experience-refresh-report.json')
    ]:
        if '--only-stack' in sys.argv and method!='StackCopiesVerification':continue
        if '--release-checks' in sys.argv and method not in ['StackCopiesVerification','QuestGripPlacementVerification']:continue
        state(method)
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/stack-'+method+'.log'))],cwd=CLONE,check=True)
        result=json.loads((CLONE/'Tools/Verification'/report).read_text())
        shutil.copy2(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
    if '--verify-only' not in sys.argv:
        state('building-phone-and-quest')
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
