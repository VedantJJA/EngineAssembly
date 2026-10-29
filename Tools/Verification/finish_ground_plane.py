"""Verify the authored Vuforia Ground Plane scenes and export the phone player."""
import ctypes,hashlib,json,os,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/ground-plane-release-status.json'
def state(stage,error=''):STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))
def deliver(source,target):
    temporary=target.with_name(target.name+'.new')
    with source.open('rb') as incoming,temporary.open('wb') as outgoing:shutil.copyfileobj(incoming,outgoing)
    os.replace(temporary,target)
def fingerprint():
    paths=[p for p in (CLONE/'Assets/Scripts').rglob('*.cs') if 'Editor' not in p.relative_to(CLONE/'Assets/Scripts').parts]
    paths += [CLONE/'Assets/Scenes'/name for name in ['MobileAssemblyAR.unity','MobileDisplayAR.unity']]
    paths += [CLONE/'Assets/Resources/VuforiaConfiguration.asset']
    return {str(p.relative_to(CLONE)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
try:
    if '--wait' in sys.argv:
        state('waiting-for-current-export')
        kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.OpenProcess.restype=ctypes.c_void_p;kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32];kernel.CloseHandle.argtypes=[ctypes.c_void_p]
        handle=kernel.OpenProcess(0x100000,False,int(sys.argv[sys.argv.index('--wait')+1]))
        if handle:kernel.WaitForSingleObject(handle,0xffffffff);kernel.CloseHandle(handle)
    built=fingerprint()
    state('preparing');shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    for name in ['MobileAssemblyAR.unity','MobileDisplayAR.unity']:deliver(ROOT/'Assets/Scenes'/name,CLONE/'Assets/Scenes'/name)
    before=fingerprint()
    for method,report in [('MobileARVerification','mobile-ar-report.json'),('PhoneARPresentationVerification','phone-ar-presentation-report.json')]:
        if method=='MobileARVerification' and '--resume-presentation' in sys.argv:
            result=json.loads((CLONE/'Tools/Verification'/report).read_text());assert result['passed'],result.get('error',method);deliver(CLONE/'Tools/Verification'/report,ROOT/'Tools/Verification'/('ground-plane-'+report));continue
        state(method);started=time.time()
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/ground-plane-'+method+'.log'))],cwd=CLONE,check=True)
        output=CLONE/'Tools/Verification'/report;assert output.exists() and output.stat().st_mtime>=started,'No current report for '+method
        result=json.loads(output.read_text());deliver(output,ROOT/'Tools/Verification'/('ground-plane-'+report));assert result['passed'],result.get('error',method)
    for path in (CLONE/'Tools/Verification').glob('Mobile*AR-*.png'):
        for folder in ['Tools/Verification','Docs/Previews']:deliver(path,ROOT/folder/('ground-plane-'+path.name))
    if '--test-only' not in sys.argv:
        reusable='--reuse-build' in sys.argv and built==before==fingerprint()
        if not reusable:
            state('building-phone')
            subprocess.run([UNITY,'-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.VuforiaAndroidBuild.RunBatch','-logFile',str(ROOT/'Tools/Verification/ground-plane-phone-build.log')],cwd=CLONE,check=True)
        for name in ['EngineAssembly-AR.apk','EngineAssembly-Phone.apk']:deliver(CLONE/'Builds/Android/EngineAssembly-AR.apk',ROOT/'Builds/Android'/name)
        subprocess.run([sys.executable,str(ROOT/'Tools/Verification/verify_android_apk.py'),str(ROOT/'Builds/Android/EngineAssembly-Phone.apk'),str(ROOT/'Tools/Verification/ground-plane-apk-report.json')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:state('failed',str(error));raise
