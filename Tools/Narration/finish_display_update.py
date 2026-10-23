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
    for pid in sys.argv[1:]: wait(pid)
    state('verifying-final-display-controls')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Audio/Narration/English']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
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
