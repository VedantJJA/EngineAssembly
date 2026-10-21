# Spatial fallback experiences

The main menu opens the Vuforia mobile assembly and display scenes, with `MobileAssemblyARFallback` and `MobileDisplayARFallback` available when AR cannot start. `RepairVRFallback` remains a desktop prototype for future VR support. See `VuforiaAR.md` for the AR setup.

On PC, **Left / Right Arrow** selects the main-engine lift or one of the current chapter's subassembly lifts. The active station is white; other stations are grey. Hold **Up / Down Arrow** to raise or lower its workpiece, including down to ground level. Lift bases have a fixed height and thickness; the deck and hydraulic column follow the workpiece. The main engine always has its own lift. Completed units lose their separate station once installed.

**F** lowers the selected workpiece below the floor, rotates it through 180 degrees around its geometry centre, and brings it back to its previous height. An empty fixture uses its base component's target geometry, so off-centre CAD origins do not make the head swing around its edge. Collisions and placement are suspended during the 1.75-second transition and restored afterwards. This also works while carrying an unrelated part. Original part scale is preserved. Assembly order remains enforced; Hard difficulty still requires the held part to align with its socket.

**Next Step** immediately installs one component with normal socket reservation, dependency checks, completion events, and chapter progress. On PC use **N** or **Escape → Next Step**. On mobile tap **Next Step** above the part tray. Manual placement is also immediate after the usual eligibility and visibility checks. Travel animation belongs to the Engine Display timeline: components appear already oriented just outside their fitting positions and use a short final insertion stroke. Complete subassemblies likewise appear beside their engine mounting positions. The assist does not skip repair removals or modify editor previews.

**V** toggles the target pointer. Offscreen targets use a pointer at the screen border. Visible targets have a hovering horizontal or vertical pointer at the actual geometry centre. After pickup, it points toward the fitting position and reflects the insertion direction. This pointer does not change the visibility checks used by manual placement.

Mobile assembly: choose an eligible card, then tap its visible glowing target. Use one finger to rotate, two fingers to pan, and pinch to zoom. Hints remain on and visible through geometry; manual placement still needs a clear view of the target. Each piston has its own workspace. Captions name the next component; Next Step removes its card only after installation finishes. The display scene keeps its assembly timeline and part information cards.

Beginner captions identify the part, explain where it goes, and name its mating component. The valve captions stay with one cylinder/valve stack through seat, seal, spring, retainer and keepers. `Docs/AssemblyNarration.txt` contains the fuller TTS script; `Docs/RepairNarration.txt` contains the same fourteen repair instructions used in game. Regenerate both and the learning resource with `Tools/EngineModel/build_learning_content.py`.

`SpatialExperienceRig` provides a content anchor and a fallback camera rig. A future XR provider can call `BeginTrackedSession(camera, anchorPose)` and `EndTrackedSession()`. Mobile controls then use the provider's camera for picking and stop repositioning it. Reset View preserves the relocated anchor. No AR/VR SDK dependency is needed to run the fallbacks.

The repair fallback starts the complete engine's 14-step timing idler bearing replacement scenario. It keeps the normal workshop, a distinct replacement bearing, instructions, and desktop interaction. Its fallback locomotion rig and content anchor are separate so they can be connected to an XR Origin and controller interaction later.

To generate missing fallback scenes in Unity, use **Tools → Engine Assembly → Create AR and VR Fallback Scenes**. The builder creates separate scene assets and registers them in Build Settings without overwriting the original scenes.
