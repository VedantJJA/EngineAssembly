"""Recheck the final display picker snapshot after the serial player exports."""
import ctypes,json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject';STATUS=ROOT/'Tools/Verification/context-display-release-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    state('waiting-for-exports');kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(sys.argv[1]))
    if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
    base=json.loads((ROOT/'Tools/Verification/context-display-status.json').read_text());assert base['stage']=='complete',base
    assert all(p.read_bytes()==(CLONE/p.relative_to(ROOT)).read_bytes() for p in (ROOT/'Assets/Scripts').rglob('*.cs')),'Export source mismatch'
    state('verifying-final-xray-picker');started=time.time();process=subprocess.run(['C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe','-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.ContextDisplayVerification.RunBatch','-logFile',str(ROOT/'Tools/Verification/context-final-display.log')],cwd=CLONE)
    output=CLONE/'Tools/Verification/context-display-report.json';assert output.stat().st_mtime>=started,'Missing final report';result=json.loads(output.read_text());shutil.copy2(output,ROOT/'Tools/Verification/context-display-report.json');assert result['passed'],result['error']
    for name in ['context-motion-pc.png','context-motion-phone.png']:
        for folder in ['Tools/Verification','Docs/Previews']:shutil.copy2(CLONE/'Tools/Verification'/name,ROOT/folder/name)
    state('complete')
except Exception as error:state('failed',str(error));raise
