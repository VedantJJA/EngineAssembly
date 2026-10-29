"""Finish clean tutorial speech, validate contextual narration/display and export."""
import ctypes,json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/context-display-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
def wait(pid):
    kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(pid))
    if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
def script(name):subprocess.run([sys.executable,str(ROOT/name)],cwd=ROOT,check=True)
try:
    state('waiting-for-clean-tutorial-voice')
    for arg in sys.argv[1:]:
        if arg.isdigit():wait(arg)
    if json.loads((ROOT/'Tools/Narration/progress.json').read_text())['status']!='complete':script('Tools/Narration/generate_narration.py')
    manifest=json.loads((ROOT/'Assets/Audio/Narration/English/manifest.json').read_text());assert not any(c.get('practicePlaybackRate') for c in manifest['clips']),'Processed tutorial speech remains'
    state('verifying-clean-audio');script('Tools/Narration/verify_audio.py');script('Tools/Narration/align_narration.py')
    for folder in ['Assets/Scripts','Assets/Resources','Assets/Shaders','Assets/Audio/Narration/English','Assets/Art/Materials/Engine','Assets/Art/Textures/ambientCG']:shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,report in [('ContextDisplayVerification','context-display-report.json'),('GuidancePickupVerification','guidance-pickup-report.json'),('PracticePacingVerification','practice-pacing-report.json'),('QuestGripPlacementVerification','quest-grip-placement-report.json'),('StackCopiesVerification','stack-copies-report.json')]:
        state(method);started=time.time();process=subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/context-'+method+'.log'))],cwd=CLONE)
        output=CLONE/'Tools/Verification'/report
        if not output.exists() or output.stat().st_mtime<started:process.check_returncode();raise RuntimeError('No current report: '+report)
        result=json.loads(output.read_text());shutil.copy2(output,ROOT/'Tools/Verification'/report)
        if not result['passed']:raise RuntimeError(result.get('error',report+' failed'))
    for name in ['context-motion-pc.png','context-motion-phone.png']:
        for folder in ['Tools/Verification','Docs/Previews']:shutil.copy2(CLONE/'Tools/Verification'/name,ROOT/folder/name)
    for source in (ROOT/'Assets/Scripts').rglob('*.cs'):
        target=Path(str(source)+'.meta');generated=Path(str(CLONE/source.relative_to(ROOT))+'.meta')
        if not target.exists() and generated.exists():shutil.copy2(generated,target)
    if '--test-only' not in sys.argv:
        state('exporting-phone-and-quest');script('Tools/Narration/finalize_experiences.py')
    state('complete')
except Exception as error:state('failed',str(error));raise
