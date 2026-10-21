# Vuforia mobile AR

The Android experience cards now open `MobileAssemblyAR` and `MobileDisplayAR`.
Both use Vuforia Engine 11.4.4 Ground Plane, with no printed marker required.
The SDK is installed from the official tarball inside the locally downloaded
Asset Store package. Its sample project settings and sample scenes were not imported.

## Licence and device testing

Your supplied app licence key is saved in Unity's Vuforia configuration, and the
SDK confirms that its developer agreement has already been accepted. To change
the key, use **Tools → Engine Assembly → AR → Open Vuforia Licence Settings**.
The configuration uses delayed initialization:
the main menu and ordinary phone/PC scenes do not start a tracking camera.
The integration checks the SDK's existing agreement state; it does not silently
accept legal terms on your behalf. Device licence validation still occurs when
Vuforia initializes on a phone.

Build for Android, allow camera access, and scan a textured, well-lit floor.
Tap **Place engine** to anchor the floating model at working height. AR creates
no table or workbench geometry. Piston stations remain beside the engine and
all builds stay above the elevated workspace plane. One finger rotates the
active build, two fingers move it, and pinch scales it. Inactive stations and
the tracked camera stay fixed. **Reposition** preserves assembly progress.
**Phone view** opens the corresponding normal mobile experience.

The **ARROW ON/OFF** card toggles the desktop-style quest pointer. It points
toward the next ghost, even before selecting its part card. On screen it hovers
beside the target and uses its insertion axis; off screen it points from the
screen edge. The arrow follows the tracked camera and hides while interaction
is blocked. Placement still requires selection and direct sight of the ghost.

CAD meshes keep their original asset data. Source pieces and ghosts follow
only their destination build's gesture scale. Completed inactive subassemblies
stay visible. Reset View affects only the active build. Display retains the
timeline and short insertion animations; assembly uses direct placement.

## Fallback

The runtime Plane Finder explicitly creates both hit-test UnityEvents before
adding listeners or enabling the component. Runtime-added SDK components do not
have the event objects serialized in Vuforia's supplied prefab. Startup catches
provider/setup exceptions and runs an independent 30-second initialization
watchdog. Camera-permission requests show their own status, detect authorization
even if a callback is missed, and distinguish permission denial from unsupported
hardware. A stalled SDK opens the matching phone fallback instead of leaving the
checking screen indefinitely. The Phone view control remains available throughout.

Vuforia's runtime anchor support check determines whether Ground Plane is available.
Unsupported hardware receives: **Your device does not support AR. Opening the standard
phone view.** Assembly falls back to `MobileAssemblyARFallback`; display falls back
to `MobileDisplayARFallback`. Both remain usable without AR.

Camera denial, missing/invalid licence and initialization failure also open phone
view, with separate messages. These failures do not claim the hardware is unsupported.
Desktop launches open phone view automatically. Temporary tracking loss pauses
placement and automatic assembly until tracking recovers.

The Android build postprocessor marks camera, autofocus and AR hardware optional in the generated
Gradle manifest so the fallback is available to devices without AR hardware.
It does not replace Unity's activity configuration. A camera usage description is
set for iOS; Android is the requested deployment target.

Android builds use IL2CPP, ARM64 and OpenGL ES 3. The SDK requires Android 10
(API 29) or newer, including for installation of this combined AR/fallback app.
ARCore is optional: an eligible phone can use Vuforia's supported alternative
tracking, while an unsupported device opens phone view.

## Verified Android build

The installable development build is `Builds/Android/EngineAssembly-AR.apk`
(approximately 140 MiB, including the narration library), with Android application identifier `com.engineassembly.app`.
Open an assembly or display experience from the main menu, allow camera access,
scan the floor and tap **Place engine**. The **Phone view** control also lets you
choose the normal mobile experience manually.

The native Android build succeeded. All 22 checks on the actual APK passed:
merged optional-hardware declarations, camera permission, ARCore metadata, ARM64
Vuforia/ARCore/IL2CPP libraries, registered AR/fallback scenes, archive integrity
and APK signing. The latest interaction checks cover 31 cases for engine scale,
PC/mobile orientation rules, audio playback, inventory collection, valve hints
and crosshair rendering. The garage and phone checks cover 9,684 assertions,
including all chapter supply layouts and the full repair sequence. Previous
presentation and menu reports are retained. The latest 105 AR-flow checks exercise
runtime Plane Finder event creation, synchronous startup failures, missing ready
callbacks, unsupported hardware, permission failure, tracked placement, tracking
recovery and both phone fallbacks, elevated floating placement, active-build-only
gestures, centered piston pivots, and all three piston subassemblies through installation.
Reports are in `Tools/Verification/android-ar-build-report.json`,
`android-apk-report.json`, `interaction-report.json`, `garage-phone-report.json`
and `vuforia-activation-report.json`.

No physical Android phone was available for this validation. Real camera imagery,
surface detection, device-side licence validation and tracking still need a phone
test; the editor's deterministic tracking provider does not verify those features.

## Editor maintenance

- **Configure Vuforia SDK** enables the SDK compilation symbol only when the package
  is registered, and saves delayed initialization.
- **Create Vuforia Mobile Scenes** creates or configures the two dedicated AR scenes
  and registers them in Build Settings. The original mobile scenes are preserved.
- `MobileARVerification.RunBatch` checks scene routing, unsupported-device and
  permission fallback messages, anchoring, scale consistency, tracking interruption,
  gestures, and the optional-hardware manifest in the isolated verification project.

The deterministic verification provider is editor-only and never added to shipped
scenes. Actual camera imagery, surface detection, device support and tracking quality
must be tested on physical phones with a valid app licence.

Official references: [Ground Plane](https://developer.vuforia.com/library/vuforia-engine/environments/ground-plane/ground-plane/),
[Unity package](https://developer.vuforia.com/library/vuforia-engine/unity-extension/vuforia-engine-package-unity/),
[Vuforia configuration](https://developer.vuforia.com/references/unity/classVuforia_1_1VuforiaConfiguration_1_1GenericVuforiaConfiguration.html),
[Android feature filtering](https://developer.android.com/guide/topics/manifest/uses-feature-element).
