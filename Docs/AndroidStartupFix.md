# Android phone startup

The reported device is a Galaxy A9 (2018); an earlier build ran successfully.
The latest user-built APK was found at `C:/Users/Vedant/Downloads/engine.apk`.
It used `com.engineassembly.quest` and its packaged `boot.config` contained:

```
xrsdk-pre-init-library=UnityOpenXR
xr-use-vulkan-offscreen-swapchain-no-main-display-buffer=1
```

XR automatic initialization was disabled, but an OpenXR loader remained assigned
to Android. The installed XR Management package's `XRGeneralBuildProcessor`
writes a native pre-initialization library whenever a loader is assigned,
regardless of `InitManagerOnStart`. This starts native VR code before any scene
or managed AR fallback can handle failure. It is a startup configuration defect;
without the phone's crash log it is not proof of the exact native crash site.

`PhoneAndroidBuildSettings` runs before the XR build processors. Ordinary phone
builds remove the Android XR loaders and stale VR boot settings, use the phone
app identifier and OpenGL ES 3, and disable both Quest launcher features.
Vuforia still initializes on demand when an AR experience is selected.

`QuestAndroidBuild.RunBatch` explicitly opts into the separate Quest path and
restores phone configuration afterward. The current editor is configured for
phone builds. The phone settings can also be reapplied from
**Tools > Engine Assembly > Android > Configure Phone (AR and standard view)**.

The APK verifier checks the packaged boot configuration, launcher, ARM64 native
libraries, optional camera/AR requirements, scene registration, archive integrity
and signature. These checks do not substitute for launching on the Galaxy A9.
`Tools/Verification/android-startup-before.json` preserves the old startup flags.

The rebuilt phone APK is `Builds/Android/EngineAssembly-Phone.apk` (the existing
`EngineAssembly-AR.apk` path also receives the same build). All 27 packaged-APK
checks passed, including exclusion of `libUnityOpenXR.so` and `libopenxr_loader.so`.
See `Tools/Verification/android-apk-report.json` for the APK hash and results.
The Galaxy A9 was not connected during verification, so the on-device launch
and exact crash site remain unverified.

For a remaining device crash, connect the phone with USB debugging enabled and
capture `adb logcat -b crash -d` immediately after reproducing it. A native crash
before the first scene cannot be diagnosed from the Unity Editor console alone.
