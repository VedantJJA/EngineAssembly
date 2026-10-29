"""Verify platform teaching and guidance settings, then package updated players."""
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
STATUS=ROOT/('Tools/Verification/teaching-portrait-status.json' if '--portrait-only' in sys.argv else 'Tools/Verification/teaching-release-status.json')

def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

try:
    if '--wait' in sys.argv:
        state('waiting-for-release-builds');process_id=int(sys.argv[sys.argv.index('--wait')+1]);kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
        handle=kernel.OpenProcess(0x100000,False,process_id)
        if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
        release=json.loads((ROOT/'Tools/Verification/teaching-release-status.json').read_text())
        if release['stage']!='complete':raise RuntimeError('Release builds did not finish successfully')
    state('preparing')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Audio/Narration/English']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,report in [
        ('TeachingGuidanceVerification','teaching-guidance-report.json'),
        ('QuestGripPlacementVerification','quest-grip-placement-report.json'),
        ('PlatformPresentationVerification','platform-presentation-report.json'),
        ('ExperienceRefreshVerification','experience-refresh-report.json'),
        ('EngineOperationVerification','engine-operation-report.json')
    ]:
        if '--portrait-only' in sys.argv:continue
        if '--resume' in sys.argv and method!='EngineOperationVerification':
            result=json.loads((ROOT/'Tools/Verification'/report).read_text())
            if not result['passed']:raise RuntimeError('Cannot resume with a failed report: '+report)
            continue
        state(method)
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/teaching-'+method+'.log'))],cwd=CLONE,check=True)
        result=json.loads((CLONE/'Tools/Verification'/report).read_text());shutil.copy2(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
        if method=='TeachingGuidanceVerification':
            for file in (CLONE/'Tools/Verification').glob('teaching-*.png'):
                for destination in ['Tools/Verification','Docs/Previews']:
                    shutil.copy2(file,ROOT/destination/file.name)
    state('verifying-phone-and-ar-movement-portraits')
    shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.PlatformPresentationVerification.RunMovementPortrait','-logFile',str(ROOT/'Tools/Verification/teaching-movement-portrait.log')],cwd=CLONE,check=True)
    portrait=json.loads((CLONE/'Tools/Verification/movement-portrait-report.json').read_text());shutil.copy2(CLONE/'Tools/Verification/movement-portrait-report.json',ROOT/'Tools/Verification/movement-portrait-report.json')
    if not portrait['passed']:raise RuntimeError(portrait.get('error','Portrait verification failed'))
    (ROOT/'Tools/Verification/teaching-portrait-status.json').write_text(json.dumps(dict(stage='complete',error=''),indent=2))
    for capture in portrait['captures']:
        for destination in ['Tools/Verification','Docs/Previews']:
            shutil.copy2(CLONE/capture['file'],ROOT/destination/Path(capture['file']).name)
    if '--portrait-only' not in sys.argv:
        state('building-phone-and-quest')
        subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
