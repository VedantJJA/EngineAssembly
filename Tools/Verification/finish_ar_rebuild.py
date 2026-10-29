"""Verify the clean Ground Plane scenes and export a matching phone APK."""
import hashlib,json,os,shutil,subprocess,sys,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
CLONE=ROOT/'Tools/Verification/UnityProject'
UNITY='C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS=ROOT/'Tools/Verification/ar-rebuild-status.json'

def state(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

def deliver(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    temporary=target.with_name(target.name+'.new')
    with source.open('rb') as incoming,temporary.open('wb') as outgoing:
        shutil.copyfileobj(incoming,outgoing)
    os.replace(temporary,target)

def snapshot(project):
    files=list((project/'Assets/Scripts').rglob('*.cs'))
    files += [project/'Assets/Scenes'/name for name in ['MobileAssemblyAR.unity','MobileDisplayAR.unity']]
    return {str(path.relative_to(project)):hashlib.sha256(path.read_bytes()).hexdigest() for path in files}

try:
    state('syncing-clean-rebuild')
    shutil.copytree(ROOT/'Assets/Scripts',CLONE/'Assets/Scripts',dirs_exist_ok=True)
    for name in ['MobileAssemblyAR.unity','MobileDisplayAR.unity']:
        deliver(ROOT/'Assets/Scenes'/name,CLONE/'Assets/Scenes'/name)
    before=snapshot(CLONE)
    jobs=[('VuforiaGroundPlaneRebuildVerification','ar-rebuild-scene-report.json'),('MobileARVerification','mobile-ar-report.json'),('PhoneARPresentationVerification','phone-ar-presentation-report.json')]
    for method,report in jobs:
        state(method);started=time.time()
        subprocess.run([UNITY,'-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.'+method+'.RunBatch','-logFile',str(ROOT/('Tools/Verification/ar-rebuild-'+method+'.log'))],cwd=CLONE,check=True)
        output=CLONE/'Tools/Verification'/report
        assert output.exists() and output.stat().st_mtime>=started,'Missing fresh result: '+method
        result=json.loads(output.read_text())
        deliver(output,ROOT/'Tools/Verification'/('ar-rebuild-'+report if not report.startswith('ar-rebuild-') else report))
        assert result['passed'],result.get('error',method)
    # The scene test deliberately reconstructs the scenes. Ship those tested
    # scenes back to the main project as well as using them in the APK.
    for name in ['MobileAssemblyAR.unity','MobileDisplayAR.unity']:
        deliver(CLONE/'Assets/Scenes'/name,ROOT/'Assets/Scenes'/name)
    for path in (CLONE/'Tools/Verification').glob('Mobile*AR-*.png'):
        deliver(path,ROOT/'Docs/Previews'/('ar-rebuild-'+path.name))
    state('building-phone')
    subprocess.run([UNITY,'-batchmode','-force-d3d11','-buildTarget','Android','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.VuforiaAndroidBuild.RunBatch','-logFile',str(ROOT/'Tools/Verification/ar-rebuild-phone-build.log')],cwd=CLONE,check=True)
    assert snapshot(ROOT)==snapshot(CLONE),'Source changed during verification/build; APK would not match workspace'
    for name in ['EngineAssembly-AR.apk','EngineAssembly-Phone.apk']:
        deliver(CLONE/'Builds/Android/EngineAssembly-AR.apk',ROOT/'Builds/Android'/name)
    subprocess.run([sys.executable,str(ROOT/'Tools/Verification/verify_android_apk.py'),str(ROOT/'Builds/Android/EngineAssembly-Phone.apk'),str(ROOT/'Tools/Verification/ar-rebuild-apk-report.json')],cwd=ROOT,check=True)
    state('complete')
except Exception as error:
    state('failed',str(error));raise
