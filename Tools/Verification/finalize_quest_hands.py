"""Preserve the in-flight phone build, verify pause-safe gestures, export Quest."""
import ctypes,json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/quest-hands-release-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
def run(script,*args):subprocess.run([sys.executable,str(ROOT/script),*map(str,args)],cwd=ROOT,check=True)
try:
    state('finishing-current-phone-build')
    kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(sys.argv[1]))
    if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
    report=json.loads((CLONE/'Tools/Verification/android-ar-build-report.json').read_text())
    if not report['passed']:raise RuntimeError(report.get('error','Phone build failed'))
    phone=ROOT/'Builds/Android/EngineAssembly-AR.apk';shutil.copy2(CLONE/'Builds/Android/EngineAssembly-AR.apk',phone);shutil.copy2(phone,phone.with_name('EngineAssembly-Phone.apk'));run('Tools/Verification/verify_android_apk.py',phone)
    state('verifying-pause-safe-hands')
    shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.QuestHandsVerification.RunBatch','-logFile',str(ROOT/'Tools/Verification/hands-final-verification.log')],cwd=CLONE,check=True)
    check=CLONE/'Tools/Verification/quest-hands-report.json';result=json.loads(check.read_text());shutil.copy2(check,ROOT/'Tools/Verification/quest-hands-report.json')
    if not result['passed']:raise RuntimeError(result.get('error','Hand validation failed'))
    for destination in ['Tools/Verification','Docs/Previews']:shutil.copy2(CLONE/'Tools/Verification/quest-visible-hands.png',ROOT/destination/'quest-visible-hands.png')
    state('building-final-quest')
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.QuestAndroidBuild.RunBatch','-logFile',str(ROOT/'Tools/Verification/complete-voices-quest.log')],cwd=CLONE,check=True)
    quest=ROOT/'Builds/Quest/EngineAssembly-Quest2.apk';shutil.copy2(CLONE/'Builds/Quest/EngineAssembly-Quest2.apk',quest);run('Tools/Verification/verify_quest_apk.py',quest)
    for source in (ROOT/'Assets/Scripts').rglob('*.cs'):
        meta=Path(str(source)+'.meta');export=CLONE/source.relative_to(ROOT)
        if not meta.exists() and Path(str(export)+'.meta').exists():shutil.copy2(Path(str(export)+'.meta'),meta)
    for folder in ['Assets/XR/Settings']:
        shutil.copytree(CLONE/folder,ROOT/folder,dirs_exist_ok=True)
    state('complete')
    (ROOT/'Tools/Narration/experience-export-status.json').write_text(json.dumps(dict(stage='complete',error='',updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
except Exception as error:state('failed',str(error));raise
