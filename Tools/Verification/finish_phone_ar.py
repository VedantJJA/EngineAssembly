"""Verify phone AR placement and responsive UI, then export the phone APK."""
import ctypes,json,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/phone-ar-release-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
try:
    if '--wait' in sys.argv:
        state('waiting-for-current-verification')
        kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
        handle=kernel.OpenProcess(0x100000,False,int(sys.argv[sys.argv.index('--wait')+1]))
        if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
    state('preparing');shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    for method,report in [('PhoneARPresentationVerification','phone-ar-presentation-report.json'),('MobileARVerification','mobile-ar-report.json'),('PracticePacingVerification','practice-pacing-report.json')]:
        if '--presentation-only' in sys.argv and method!='PhoneARPresentationVerification':continue
        state(method);started=time.time()
        process=subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/phone-ar-'+method+'.log'))],cwd=CLONE)
        path=CLONE/'Tools/Verification'/report
        assert path.exists() and path.stat().st_mtime>=started,'No fresh report for '+method
        result=json.loads(path.read_text());shutil.copy2(path,ROOT/'Tools/Verification'/report);assert result['passed'],result.get('error',method)
    for path in (CLONE/'Tools/Verification').glob('Mobile*AR-*.png'):
        for folder in ['Tools/Verification','Docs/Previews']:shutil.copy2(path,ROOT/folder/path.name)
    for path in (ROOT/'Assets/Scripts').rglob('*.cs'):
        target=Path(str(path)+'.meta');source=Path(str(CLONE/path.relative_to(ROOT))+'.meta')
        if not target.exists() and source.exists():shutil.copy2(source,target)
    if '--test-only' not in sys.argv:
        state('building-phone')
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.VuforiaAndroidBuild.RunBatch','-logFile',str(ROOT/'Tools/Verification/phone-ar-android-build.log')],cwd=CLONE,check=True)
        for name in ['EngineAssembly-AR.apk','EngineAssembly-Phone.apk']:shutil.copy2(CLONE/'Builds/Android/EngineAssembly-AR.apk',ROOT/'Builds/Android'/name)
        subprocess.run([sys.executable,str(ROOT/'Tools/Verification/verify_android_apk.py'),str(ROOT/'Builds/Android/EngineAssembly-Phone.apk')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:state('failed',str(error));raise
