"""Verify the final display controls, then export the phone and Quest players."""
import ctypes, json, shutil, subprocess, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLONE = ROOT / 'Tools/Verification/UnityProject'
UNITY = 'C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS = ROOT / 'Tools/Narration/experience-export-status.json'

def state(stage, error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

def wait(pid):
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(pid))
    if handle:
        kernel.WaitForSingleObject(handle,0xffffffff)
        kernel.CloseHandle(handle)

try:
    state('finishing-current-build')
    for pid in sys.argv[1:]:
        if not pid.startswith('--'): wait(pid)
    state('verifying-final-display-controls')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Audio/Narration/English']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    if '--repair' in sys.argv:
        state('verifying-mobile-repair-and-experiences')
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.ExperienceRefreshVerification.RunBatch','-logFile',str(ROOT/'Tools/Verification/repair-hotbar-verification.log')],cwd=CLONE,check=True)
        repair_report=json.loads((CLONE/'Tools/Verification/experience-refresh-report.json').read_text())
        if not repair_report['passed']: raise RuntimeError(repair_report['error'])
        shutil.copy2(CLONE/'Tools/Verification/experience-refresh-report.json',ROOT/'Tools/Verification/experience-refresh-report.json')
        state('verifying-final-display-controls')
    state('verifying-tracked-grip-release-and-visibility')
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.QuestGripPlacementVerification.RunBatch','-logFile',str(ROOT/'Tools/Verification/platform-grip-release-final.log')],cwd=CLONE,check=True)
    grip=json.loads((CLONE/'Tools/Verification/quest-grip-placement-report.json').read_text())
    shutil.copy2(CLONE/'Tools/Verification/quest-grip-placement-report.json',ROOT/'Tools/Verification/quest-grip-placement-report.json')
    if not grip['passed']: raise RuntimeError(grip['error'])
    state('verifying-platform-layouts-and-tutorial-movement')
    presentation=json.loads((CLONE/'Tools/Verification/platform-presentation-report.json').read_text())
    shutil.copy2(CLONE/'Tools/Verification/platform-presentation-report.json',ROOT/'Tools/Verification/platform-presentation-report.json')
    if not presentation['passed']: raise RuntimeError(presentation['error'])
    for capture in presentation['captures']:
        for destination in ['Tools/Verification','Docs/Previews']:
            shutil.copy2(CLONE/capture['file'],ROOT/destination/Path(capture['file']).name)
    state('verifying-final-display-controls')
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.EngineOperationVerification.RunBatch','-logFile',str(ROOT/'Tools/Verification/operation-final-replay.log')],cwd=CLONE,check=True)
    report=json.loads((CLONE/'Tools/Verification/engine-operation-report.json').read_text())
    if not report['passed']: raise RuntimeError(report['error'])
    shutil.copy2(CLONE/'Tools/Verification/engine-operation-report.json',ROOT/'Tools/Verification/engine-operation-report.json')
    for name in ['display-info-caption','display-motion-controls','engine-running-section']:
        for destination in ['Tools/Verification','Docs/Previews']:
            shutil.copy2(CLONE/f'Tools/Verification/{name}.png',ROOT/f'{destination}/{name}.png')
    subprocess.run([sys.executable,str(ROOT/'Tools/Narration/finalize_experiences.py')],cwd=ROOT,check=True)
except Exception as error:
    state('failed',str(error))
    raise
