# Assembly workshop

Open `Assets/Scenes/AssemblyWorkshop.unity` and press Play. The scene uses the labeled three-cylinder FBX, 318 parts, and nine chapters. Existing scenes are retained; this is the scene configured for the new workflow.

## Two modes

**Assembly** lets you choose a chapter, install that chapter's parts, and continue after completing it. Earlier chapters are placed automatically as context when you choose a later chapter. This is a chapter-practice workflow, not a persistent chapter-unlock campaign. Choosing/restarting a chapter resets that chapter's progress. Recipes are authoring data; they do not save a player's current game progress.

**Edit** previews the selected chapter assembled and lets you change priorities and required workpiece orientation. Use the in-game JSON library to load recipes or save edits. Select a part, take it into the workshop to position it, return with Tab, then capture either its loose tray pose or target pose. Apply/preview edits to rebuild the current chapter from the edited recipe. Saving validates the recipe first.

The dedicated Unity window at **Tools > Engine Assembly > Chapter Editor** offers more detailed authoring outside Play mode: chapter names/order, moving parts between chapters, exact target/tray poses, dependencies, socket parenting, interchangeable geometry IDs, snap radius/angle, axial/free rotation, and rotational symmetry. Select or frame a chapter's objects from this window. JSON saves made in Play mode persist after leaving Play mode.

JSON source files live in `Assets/AssemblyRecipes`. The generated Resources catalog includes these files in player builds. In a built app, exported/edited files go to `Application.persistentDataPath/AssemblyRecipes`, because the project's Assets directory is not writable there. The library lists both built-in recipes and local exports. Export is the Save JSON action; imported files can be copied into the Assets folder or opened through the Chapter Editor's Import JSON button.

## Controls

| Input | Action |
| --- | --- |
| Tab | Open/close the workshop panel |
| Escape | Open the panel and release the cursor |
| WASD / Shift | Walk / walk faster |
| Mouse | Look |
| E or left click | Pick up or release the highlighted part/platform |
| Hold right mouse and move | Rotate the held object |
| Mouse wheel | Move a held object closer/farther |
| G | Release the held object |
| F | Flip the workpiece, or the held platform, by 180 degrees |

Use **Move workpiece** in the panel to carry the entire installed assembly. **Flip workpiece** works from the panel too. A part marked as a preset base redirects pickup to the workpiece. Loose parts remain in the tray when the workpiece is moved or flipped.

## Assembly rules

- Smaller priorities assemble first. Equal priorities are an unordered step; every bolt in a chapter may share a priority.
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

Open **Tools > Engine Assembly > Batch Setup Parts**. Check scene roots, or use the Hierarchy selection. Each mesh under those roots is processed once. Choose:

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
