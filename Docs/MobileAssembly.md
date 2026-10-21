# Mobile assembly scene

Open `Assets/Scenes/MobileAssemblyARFallback.unity` and press Play for the touch fallback. Android assembly from the main menu opens the Vuforia AR scene and offers this fallback if AR cannot start. See `VuforiaAR.md` for camera and surface placement. The original `MobileAssembly` scene is also retained.

Bold instruction captions stay above the engine viewport on light cards and follow the reviewed assembly sequence. Fasteners share an instruction with a remaining group count. See `Docs/EngineLearning.md` for the narration script and the separate mobile Engine Display timeline scene.

- Tap a component card, then tap its visible glowing ghost to install it. Cards are ordered by assembly priority; later groups remain dimmed until prerequisites are complete. Components at the same priority can be installed in any valid order.
- Drag with one finger to rotate around the active engine or subassembly center. Drag with two fingers to translate it parallel to the camera image plane; pinch to zoom. Swiping the bottom strip scrolls cards without rotating the engine. A rotation gesture does not also place a part.
- FLIP turns the active workpiece over; RESET VIEW restores its starting pose within the current spatial anchor. Parts can be installed at any engine orientation, while assembly order stays enforced.
- NEXT STEP immediately installs one eligible component per tap. Manual placement is immediate too; insertion animations are reserved for Engine Display.
- Installed cards disappear. CONTINUE appears when the section is complete. CHAPTERS lets you visit another section with earlier sections already assembled.
- In the Editor, use left mouse drag/tap to emulate one finger, middle-mouse drag to emulate two-finger pan, and the mouse wheel to zoom. The mobile scene always uses Easy placement without changing the desktop difficulty preference.

The scene uses the existing three-cylinder JSON recipe with 317 playable physical components and eight complete-unit carriers. `Assets/UI/MobileParts` contains generated component thumbnails, referenced by `Assets/UI/MobilePartArtwork.asset`. The runtime UI uses custom rounded cards, a light theme, and the existing Inter font assets. No walking controller or desktop workshop menus are instantiated.

The `Floating Engine Anchor` carries the model and sockets. Rotation uses the model bounds center while preserving socket-relative targets. An offscreen storage transform holds uninstalled source objects. Selecting a card enters the existing Held state, but its source mesh stays hidden until a visible ghost is tapped or Next Step is used. The mesh is installed directly at its socket, and its card disappears immediately. Easy placement still requires selection and respects dependencies and occlusion.

Future AR integration can bind its camera and anchor through `SpatialExperienceRig.BeginTrackedSession`, preserving the assembly manager, card selection, and ghost ray-placement flow. See [SpatialFallbacks.md](SpatialFallbacks.md).

Each piston/rod unit has its own workspace. Only the active unit and its remaining assembly cards are shown. Completing one unit focuses the next workspace and hides the finished unit; after all three are built, the camera returns to the engine for their installation. The head, sump and timing/accessory idlers use the same independent workspace system. One-finger rotation and two-finger pan/pinch affect only the active workspace. RESET VIEW restores that workspace's view. MAIN MENU returns to the experience menu.

Verification: `EngineAssembly.Editor.MobileAssemblyVerification.RunBatch` runs in the isolated verification project, tests card order, selection gating, CAD ghost ray placement after rotation, card removal, chapter preparation, and artwork coverage, and captures portrait/landscape previews. Physical Android/iOS device performance and touch behavior still need device testing.

The revised recipe adds eight complete-unit installation steps. During bench steps, the active subassembly appears in the work area; once complete it becomes an installable unit card. Hints are enabled by default and visible through the model, while installation requires a visible target surface. See [Subassemblies.md](Subassemblies.md).

Small-part taps allow a margin around visible ghost surfaces. Valve seats and seals use their actual final target positions in the corrected head's open bores. Other geometry and walls still block targeting; selecting a card remains required.

