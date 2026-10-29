"""Inspect the shipped APK, including its merged manifest and native libraries."""
import hashlib
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

project = Path(__file__).resolve().parents[2]
android = Path("C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/PlaybackEngines/AndroidPlayer")
sdk = android / "SDK"
java = android / "OpenJDK/bin/java.exe"
tools = sdk / "cmdline-tools/16.0"
build_tools = sdk / "build-tools/36.0.0"
apk = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else project / "Builds/Android/EngineAssembly-AR.apk"
report = {"passed": False, "apk": str(apk), "checks": [], "error": ""}


def check(condition, description):
    if not condition:
        raise AssertionError(description)
    report["checks"].append(description)


try:
    check(apk.is_file(), "Android APK exists")
    manifest_text = subprocess.check_output([
        str(java), f"-Dcom.android.sdklib.toolsdir={tools}", "-classpath",
        str(tools / "lib/apkanalyzer-classpath.jar"),
        "com.android.tools.apk.analyzer.ApkAnalyzerCli", "manifest", "print", str(apk)
    ], text=True, encoding="utf-8")
    (project / "Tools/Verification/android-apk-manifest.xml").write_text(manifest_text, encoding="utf-8")
    manifest = ET.fromstring(manifest_text)
    attr = "{http://schemas.android.com/apk/res/android}"
    check(manifest.get("package") == "com.engineassembly.app", "APK uses the engine project's Android identifier")
    check(int(manifest.find("uses-sdk").get(attr + "minSdkVersion")) == 29, "Minimum Android version satisfies Vuforia's API 29 requirement")
    features = {item.get(attr + "name"): item for item in manifest.findall("uses-feature")}
    headset = features.get("android.hardware.vr.headtracking")
    check(headset is None or headset.get(attr + "required") == "false", "Phone APK does not require headset tracking hardware")
    check(not any(node.get(attr + "name") == "com.oculus.intent.category.VR" for node in manifest.iter("category")), "Phone APK keeps its ordinary Android launcher category")
    for name in ("camera", "camera.autofocus", "camera.any", "camera.ar"):
        feature = features.get("android.hardware." + name)
        check(feature is not None and feature.get(attr + "required") == "false", f"{name} hardware is optional in the final merged manifest")
    check(any(item.get(attr + "name") == "android.permission.CAMERA" for item in manifest.findall("uses-permission")), "Camera permission is packaged")
    app = manifest.find("application")
    check(app.get(attr + "debuggable", "false") == "false", "Phone APK is a release player without Android debugging overhead")
    check(any(item.get(attr + "name") == "com.google.ar.core" and item.get(attr + "value") == "optional" for item in app.findall("meta-data")), "ARCore remains optional after manifest merging")
    check(any("com.unity3d.player.UnityPlayer" in item.get(attr + "name", "") for item in app.findall("activity")), "Unity's Android activity is preserved")
    with zipfile.ZipFile(apk) as archive:
        boot = archive.read("assets/bin/Data/boot.config").decode("utf-8")
        check("xrsdk-pre-init-library=" not in boot, "Phone starts without native VR pre-initialization")
        check("xr-use-vulkan-offscreen-swapchain-no-main-display-buffer=1" not in boot,
              "Phone retains its normal on-screen display buffer")
        check(archive.testzip() is None, "APK archive passes CRC integrity checks")
        libraries = [name for name in archive.namelist() if name.startswith("lib/") and name.endswith(".so")]
        check(not any(name.endswith(("/libUnityOpenXR.so", "/libopenxr_loader.so")) for name in libraries),
              "Phone APK excludes native headset runtime libraries")
        check(libraries and all(name.startswith("lib/arm64-v8a/") for name in libraries), "All packaged native libraries use ARM64")
        for name in ("libVuforiaEngine.so", "libVuforiaUnityPlayer.so", "libarcore_sdk_c.so", "libil2cpp.so"):
            check(archive.read("lib/arm64-v8a/" + name).startswith(b"\x7fELF"), f"{name} is packaged as a native ELF library")
        scene_data = archive.read("assets/bin/Data/globalgamemanagers")
        for scene in ("MainMenu", "MobileAssemblyAR", "MobileDisplayAR", "MobileAssemblyARFallback", "MobileDisplayARFallback"):
            check(f"Assets/Scenes/{scene}.unity".encode() in scene_data, f"{scene} is registered in the built player")
    signing = subprocess.check_output([str(java), "-jar", str(build_tools / "lib/apksigner.jar"), "verify", "--verbose", str(apk)], text=True)
    check("Verified using v2 scheme (APK Signature Scheme v2): true" in signing, "APK signature verifies")
    report.update(passed=True, bytes=apk.stat().st_size, sha256=hashlib.sha256(apk.read_bytes()).hexdigest())
except Exception as error:
    report["error"] = str(error)

(Path(sys.argv[2]) if len(sys.argv)>2 else project / "Tools/Verification/android-apk-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
sys.exit(0 if report["passed"] else 1)
