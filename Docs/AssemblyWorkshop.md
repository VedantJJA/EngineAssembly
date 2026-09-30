# Assembly workshop

Open `Assets/Scenes/AssemblyWorkshop.unity` and press Play. The scene uses the labeled three-cylinder FBX, 318 parts, and nine chapters. Existing scenes are retained; this is the scene configured for the new workflow.

## Two modes

**Assembly** lets you choose a chapter, install that chapter's parts, and continue after completing it. Earlier chapters are placed automatically as context when you choose a later chapter. This is a chapter-practice workflow, not a persistent chapter-unlock campaign. Choosing/restarting a chapter resets that chapter's progress. Recipes are authoring data; they do not save a player's current game progress.

The chapter selector's **TASK: ASSEMBLE / DISASSEMBLE** card switches the selected section's exercise. Switching restarts that section: assembly stages its loose parts; disassembly installs the section first, then requires removal in reverse priority/dependency order. Parts from previous chapters stay assembled. Equal-priority peers can be removed in any order when no explicit dependency blocks them.

**Edit** (F6) previews any selected chapter with all previous chapters assembled. Tab opens chapter cards; F7 opens the authoring panel. Add a chapter, edit its name/instructions, assign parts using ALL PARTS and MOVE TO THIS CHAPTER, and edit assembly priorities. Empty chapters are saved as drafts and cannot be played. Play progression skips drafts. Save JSON before leaving Play mode to retain recipe changes.

In either Edit or Play, Move mode carries the whole installed engine frame when you pick up an installed part. Every socket stays attached to that frame. Moving a loose part in Edit + Move captures its tray pose. To deliberately redefine an individual assembly target, position the loose part and use the explicit CAPTURE TARGET POSE authoring action. PREVIEW reapplies the edited recipe.

The dedicated Unity window at **Tools > Engine Assembly > Chapter Editor** offers more detailed authoring outside Play mode: chapter names/order, moving parts between chapters, exact target/tray poses, dependencies, socket parenting, interchangeable geometry IDs, snap radius/angle, axial/free rotation, and rotational symmetry. Select or frame a chapter's objects from this window. JSON saves made in Play mode persist after leaving Play mode.

JSON source files live in `Assets/AssemblyRecipes`. The generated Resources catalog includes these files in player builds. In a built app, exported/edited files go to `Application.persistentDataPath/AssemblyRecipes`, because the project's Assets directory is not writable there. The library lists both built-in recipes and local exports. Export is the Save JSON action; imported files can be copied into the Assets folder or opened through the Chapter Editor's Import JSON button.

## Controls

| Input | Action |
| --- | --- |
| Tab | Open/close the chapter card selector |
| F6 | Switch between Edit and Play |
| F7 | Open chapter authoring (Edit only) |
| Escape | Pause: Continue, Chapters, Settings, Quit |
| M | Toggle Build / Move |
| H | Toggle persistent hints (enabled by default; visible through geometry) |
| Space | Jump |
| Hold Ctrl or C | Crouch (standing requires clear headroom) |
| Enter | Continue after completing a chapter |
| WASD / Shift | Walk / walk faster |
| Mouse | Look |
| E or left click | Pick up or release the highlighted part/platform |
| Hold right mouse and move | Rotate the held object; with empty hands, zoom instead |
| Mouse wheel | Move a held object closer/farther |
| G | Release the held object |
| F | Flip the workpiece, or the held platform, by 180 degrees |

Use **Move workpiece** in the authoring panel to carry the installed assembly. In Play + Move mode, clicking an installed part picks up the entire workpiece. Clicking the workbench/platform in Move mode carries it and its resting parts. Build mode handles assembly snapping; Move mode never snaps on release. F flips the base so underside steps can be completed. Loose tray parts remain separate from the engine. The sample tray positions are relative to the movable workbench, so later chapters use its new position.

Menus pause simulation and release the cursor. Switching chapter or F6 resets chapter placement; closing a menu preserves it. Editing a recipe and saving JSON persists part targets, tray poses, priorities and chapters; moving a bench/workpiece is a session placement, not a saved world-layout system.

## Difficulty and card artwork

Escape > Settings selects **Hard** (position and rotation), **Medium** (position only; rotation aligns during snapping), or **Easy** (pick up the part, then look directly at its eligible ghost and click). E/G drops an Easy-mode part without installing it. Difficulty is remembered locally. All three enforce prerequisites, assembly priority and required base orientation. Easy mode raycasts exact target and installed-part meshes in a separate physics scene, so ghost meshes never become physical obstacles and convex hulls cannot falsely block visible engine cavities. Solid environment objects still block the view. H hints remain visible through those objects, but hints never bypass placement rules.

The runtime interface uses custom clickable Canvas cards and Inter regular/semibold TextMesh Pro fonts, with no stock Button components. Inter's license is included in `Assets/UI/Fonts`. Generated chapter illustrations are in `Assets/UI/ChapterImages`.

To replace a chapter image while the game is **stopped**:

1. Put your PNG/JPG into the project's Assets folder.
2. Open **Tools > Engine Assembly > Chapter Editor**, load the JSON and select its chapter.
3. Drag the image into **Chapter card image**. The artwork asset saves immediately.

Images are referenced by recipe title and stable chapter ID in `Assets/Resources/AssemblyChapterArtwork.asset`. They are separate from portable JSON; reassign images if you rename the overall recipe title. Newly added chapters show a numbered placeholder until an image is assigned.

## Assembly rules

- Smaller priorities assemble first. Equal priorities are an unordered step. Fasteners within one fastening operation share a priority; retainers for different layers follow their required installation steps.
- Equal priority does **not** make parts interchangeable. Set the same nonempty geometry group only for genuinely compatible parts. Interchangeable sockets must also belong to the same chapter and priority.
- A socket is reserved during interpolation, then occupied on completion. Disabling the part or resetting the chapter releases its reservation.
- Snap radius is measured in world-space metres at the mating anchor. Set an optional grip/mating anchor on AssemblyPart when the CAD origin is unsuitable. The ghost and snap align this anchor to the socket.
- Exact rotation supports a configurable tolerance and rotational symmetry around the local Y axis. Axial mode ignores spin around Y but still rejects an inverted mating axis. Free mode assists all rotations.
- Snap-on-release is the default. Turn it off on AssemblyPart for automatic snapping when a held part reaches a valid socket.
- Sockets can be parented to the workpiece or to a specific part ID. Their poses are stored relative to that parent, so targets follow movement and rotation.
- Prerequisites and socket-parent dependencies must be installed first. Cycles, missing IDs, dependencies on later chapters/steps, invalid poses, and duplicate IDs are rejected before import changes the scene.
- A step can require an upright or upside-down workpiece. The sample sump steps demonstrate this. Orientation tolerance is approximately 32 degrees from upright/inverted.
- Installed parts are removed in reverse step order during Assembly mode. Explicit dependents must be removed first in both modes. Preset bases and parts from previous chapters are fixed for the selected chapter.

The public interaction methods (`BeginGrab`, `MoveHeld`, `Release`) are independent of desktop input. `PartInteractionController` is a small adapter for later XR integration. This change does not install or claim tested VR-controller bindings.

## Highlighting

The URP preview shader uses transparent fill and a Fresnel edge: blue for a target, green when placement is ready, amber for an unmet rule. Hovering and snap completion use visual-only overlays. These meshes contain no gameplay scripts, rigidbodies, or colliders. Source materials remain untouched. Depth testing prevents targets shining through every surface; stereo/instancing macros are present for later XR testing. The Resources material keeps the shader referenced in builds.

## Pickupable platforms

Add `PickupablePlatform` plus an appropriate collider to a movable bench/platform. The Rigidbody is added automatically. By default it remains kinematic on release, so it can act as a stable work surface. Its `lockWhenReleased` option allows a normal physics prop instead. Resting rigidbodies can be carried while the platform is held; their parenting and physics settings are restored on release. Parts installed on the workpiece stay attached through their sockets.

## Batch setup and colliders

Open **Tools > Engine Assembly > Batch Setup Parts**. Check scene roots, use the Hierarchy selection, or **Select all**. **Clear selection** deselects them. Each mesh under those roots is processed once. Select all includes environment meshes listed in the window; uncheck floors/tables if you only want engine parts. Choose:

| Method | Result |
| --- | --- |
| Mesh | Unity MeshCollider; use convex for movable parts |
| CoACD | Multiple convex hulls from the actual CoACD backend |
| VHACD | Multiple convex hulls from the actual V-HACD backend |

Convex decomposition runs outside the Unity editor process, with progress and cancellation. The tool caps each hull's vertices to 64 and rejects hulls exceeding the collider face limit. It stores generated meshes in `Assets/GeneratedColliders`. User colliders are disabled only after successful generation; trigger colliders are retained. Generated collider groups are identifiable and replaced on regeneration. Each completed object's scene changes can be undone. Generated mesh asset files are retained for undo/reference safety.

Enable **Read/Write** in the model importer before using decomposition. The sample workshop initially uses ordinary convex MeshColliders; choose CoACD/VHACD for parts where a single hull covers important holes or cavities. The tool does not silently substitute one backend for another. Non-convex mesh colliders are rejected on Rigidbody objects.

The local dependencies have been installed for this workspace. On another machine, select a Python executable in the batch window and run:

```text
python -m pip install --target Tools/Colliders/python_deps -r Tools/Colliders/requirements.txt
```

`Tools/Colliders/settings.local.json` stores the local Python path; dependencies and machine-specific settings are ignored by Git. A generation job times out after five minutes per mesh and leaves that mesh's existing colliders intact on failure. CAD engine meshes can take considerably longer than the small verification mesh.

Backend references: [CoACD](https://github.com/SarahWeiii/CoACD), [V-HACD Python bindings](https://github.com/trimesh/vhacdx). URP rendering checks use Unity's [render request API](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/rendering/renderpipeline/submitrenderrequest).

## Migration and verification

The old disassembly-recording and descending-order implementation was replaced. Existing script GUIDs were preserved where classes were rewritten, but old recorded JSON is not compatible with the version-1 chapter schema. The old recorded file and old scenes are retained. For another engine, batch-setup parts, add/configure AssemblyManager, capture the scene in the Chapter Editor, then author and save a recipe.

Use **Tools > Engine Assembly > Run Verification** for editor checks. `Tools/Verification/compile.ps1` compiles runtime/editor code against this project's Unity references without changing the open scene. The isolated Unity test project and its cache are ignored by Git. Check the JSON reports in `Tools/Verification` for the exact assertions executed.

The CAD model's detailed geometry and original scale remain unchanged. The rework does not optimize its polygon count or calibrate real-world AR scale. Chapter order is an educational starting point; refine the recipe for your intended teaching procedure.

See [Subassemblies.md](Subassemblies.md) for the revised version-2 process, complete-unit handling and visibility rules.

