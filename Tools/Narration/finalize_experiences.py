"""One-shot completion: wait for voices, align captions, package phone and Quest."""
import ctypes,json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
STATUS=ROOT/'Tools/Narration/experience-export-status.json'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
def status(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
def wait(pid):
    kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(pid))
    if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
def run(script,*args):subprocess.run([sys.executable,str(ROOT/script),*map(str,args)],cwd=ROOT,check=True)
try:
    status('waiting-for-voices-and-word-alignment')
    for pid in sys.argv[1:]:wait(pid)
    if json.loads((ROOT/'Tools/Narration/progress.json').read_text())['status']!='complete':
        status('resuming-missing-voices');run(Path('Tools/Narration/generate_narration.py'))
    status('verifying-all-voice-cues');run(Path('Tools/Narration/verify_audio.py'))
    status('finishing-word-alignment');run(Path('Tools/Narration/align_narration.py'))
    texts=json.loads((ROOT/'Assets/Resources/NarrationText.json').read_text())['cues']
    (ROOT/'Tools/Narration/caption-alignment-report.json').write_text(json.dumps(dict(total=len(texts),aligned=sum(bool(c.get('words')) for c in texts),estimated=[c['id'] for c in texts if not c.get('words')]),indent=2))
    status('preparing-verified-build-assets')
    for folder in ['Assets/Scripts','Assets/Shaders','Assets/Resources','Assets/Audio/Narration/English','Assets/AssemblyRecipes','Assets/UI','Assets/Settings','Tools/Android','Assets/Art/Materials/Engine','ProjectSettings']:
        shutil.copytree(ROOT/folder,CLONE/folder,dirs_exist_ok=True)
    for method,folder,name,verifier in [
        ('VuforiaAndroidBuild','Android','EngineAssembly-AR.apk','verify_android_apk.py'),
        ('QuestAndroidBuild','Quest','EngineAssembly-Quest2.apk','verify_quest_apk.py')]:
        status('building-'+folder.lower())
        command=[UNITY,'-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/complete-voices-'+folder.lower()+'.log'))]
        subprocess.run(command,cwd=CLONE,check=True)
        target=ROOT/'Builds'/folder/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(CLONE/'Builds'/folder/name,target)
        if folder=='Android':shutil.copy2(target,target.with_name('EngineAssembly-Phone.apk'))
        run(Path('Tools/Verification')/verifier,target)
    status('complete')
except Exception as error:
    status('failed',str(error));raise
