"""Verify the separate Quest APK without starting a headset."""
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

project = Path(__file__).resolve().parents[2]
android = Path('C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/PlaybackEngines/AndroidPlayer')
java = android / 'OpenJDK/bin/java.exe'
sdk = android / 'SDK'
tools = sdk / 'cmdline-tools/16.0'
apk = project / 'Builds/Quest/EngineAssembly-Quest2.apk'
report = dict(passed=False, apk=str(apk), checks=[], error='')

def check(value, label):
    if not value:
        raise AssertionError(label)
    report['checks'].append(label)

try:
    check(apk.is_file(), 'Quest APK exists')
    manifest = subprocess.check_output([str(java), f'-Dcom.android.sdklib.toolsdir={tools}', '-classpath', str(tools/'lib/apkanalyzer-classpath.jar'), 'com.android.tools.apk.analyzer.ApkAnalyzerCli', 'manifest', 'print', str(apk)], text=True, encoding='utf-8')
    root = ET.fromstring(manifest)
    attr = '{http://schemas.android.com/apk/res/android}'
    check(root.find('application').get(attr+'debuggable','false') == 'false', 'Quest APK is a release player without Android debugging overhead')
    check(root.attrib['package'] == 'com.engineassembly.quest', 'Quest has a separate application identifier from the phone build')
    check(int(root.find('uses-sdk').attrib[attr+'minSdkVersion']) >= 29, 'Quest build targets Android API 29 or newer')
    categories = {node.attrib.get(attr+'name') for node in root.iter('category')}
    check('com.oculus.intent.category.VR' in categories, 'Quest launcher includes the VR category')
    metadata = {node.attrib.get(attr+'name'):node.attrib.get(attr+'value','') for node in root.iter('meta-data')}
    check('quest2' in metadata.get('com.oculus.supportedDevices','').lower(), 'Manifest explicitly supports Quest 2')
    features = {node.attrib.get(attr+'name') for node in root.iter('uses-feature')}
    check('android.hardware.vr.headtracking' in features, 'Quest manifest declares headset tracking')
    permissions = {node.attrib.get(attr+'name') for node in root.iter('uses-permission')}
    check('com.oculus.permission.HAND_TRACKING' in permissions, 'Quest declares permission for optical hand tracking')
    hand_feature = next((node for node in root.iter('uses-feature') if node.attrib.get(attr+'name') == 'oculus.software.handtracking'), None)
    check(hand_feature is not None and hand_feature.attrib.get(attr+'required') == 'false', 'Optical hands are supported while Touch controllers remain available')
    with zipfile.ZipFile(apk) as archive:
        check(archive.testzip() is None, 'Quest APK passes archive integrity checks')
        names = archive.namelist()
        libraries = [name for name in names if name.startswith('lib/') and name.endswith('.so')]
        check(all('/arm64-v8a/' in name for name in libraries), 'Quest native libraries use ARM64')
        check(any(name.endswith('libopenxr_loader.so') for name in libraries), 'OpenXR loader is packaged')
        check(any('UnityOpenXR' in name for name in libraries), 'Unity OpenXR native runtime is packaged')
        check(any(name.endswith('libil2cpp.so') for name in libraries), 'Quest player uses IL2CPP')
        data = b''.join(archive.read(name) for name in names if name.endswith('globalgamemanagers'))
        for scene in ['MainMenu', 'RepairVRFallback', 'AssemblyWorkshop']:
            check(scene.encode() in data, f'{scene} is registered in the Quest player')
    subprocess.check_call([str(java), '-jar', str(sdk/'build-tools/36.0.0/lib/apksigner.jar'), 'verify', str(apk)], stdout=subprocess.DEVNULL)
    check(True, 'Quest APK signature verifies')
    report.update(passed=True, bytes=apk.stat().st_size, sha256=hashlib.sha256(apk.read_bytes()).hexdigest())
except Exception as error:
    report['error'] = str(error)
(project/'Tools/Verification/quest-apk-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
raise SystemExit(0 if report['passed'] else 1)
