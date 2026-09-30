# Mobile assembly scene

Open `Assets/Scenes/MobileAssembly.unity` and press Play. This is a separate, fixed-camera mobile experience; AR tracking and plane placement are not connected yet.

- Tap a component card, then tap its visible glowing ghost to install it. Cards are ordered by assembly priority; later groups remain dimmed until prerequisites are complete. Components at the same priority can be installed in any valid order.
- Drag with one finger to rotate around the active engine or subassembly center. Drag with two fingers to translate it parallel to the camera image plane. Swiping the bottom strip scrolls cards without rotating the engine. A rotation gesture does not also place a part.
- FLIP turns the engine over; RESET VIEW restores its starting pose. Chapter orientation requirements still apply.
- Installed cards disappear. CONTINUE appears when the section is complete. CHAPTERS lets you visit another section with earlier sections already assembled.
- In the Editor, use left mouse drag/tap to emulate one finger and middle-mouse drag to emulate two-finger pan. The mobile scene always uses Easy placement without changing the desktop difficulty preference.

The scene uses the existing three-cylinder JSON recipe and all 318 assembly components. `Assets/UI/MobileParts` contains generated component thumbnails, referenced by `Assets/UI/MobilePartArtwork.asset`. The runtime UI uses custom card handlers and the existing Inter font assets. No walking controller or desktop workshop menus are instantiated.

The `Floating Engine Anchor` carries the model and sockets. Rotation uses the model bounds center while preserving socket-relative targets. An offscreen storage transform holds uninstalled source objects; their renderers and colliders remain disabled until installation. A selected source enters the existing Held state so Easy placement still requires selection and respects dependencies and occlusion.

Future AR integration should replace fixed-camera framing and touch rotation with an AR camera/anchor adapter while preserving the assembly manager, card selection, and ghost ray-placement flow.

Verification: `EngineAssembly.Editor.MobileAssemblyVerification.RunBatch` runs in the isolated verification project, tests card order, selection gating, CAD ghost ray placement after rotation, card removal, chapter preparation, and artwork coverage, and captures portrait/landscape previews. Physical Android/iOS device performance and touch behavior still need device testing.

The revised recipe adds seven complete-unit installation steps. During bench steps, the active subassembly appears in the work area; once complete it becomes an installable unit card. Hints are enabled by default and visible through the model, while installation requires a visible target surface. See [Subassemblies.md](Subassemblies.md).

