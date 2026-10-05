# Captions, narration and mobile engine display

The main menu now opens three implemented experiences: PC assembly, mobile engine assembly, and mobile engine display. Repair remains pending. Mobile scenes are virtual previews; AR tracking is still a later integration.

## Assembly captions

Both workshops display bold white captions with a dark background and text shadow. The PC instruction card sits at the top right with a step number; P hides/restores it. Mobile retains the bottom caption layout. Captions do not intercept clicks. Instructions describe the mating location and component sequence, including the piston bench builds, separate rod caps, upside-down valve insertion, upright valve spring stacks and completed carrier placement. Repeated fasteners share a cue and progress count. A cue stays active until its components are installed; the next eligible component is named underneath. Captions also explain workpiece orientation when a flip is required. Hints can still be toggled off with H on PC; captions do not turn them back on.

`Docs/AssemblyNarration.txt` is the plain spoken script for later TTS. `Assets/Resources/EngineLearning.json` contains 167 individual narration/caption cues with cue IDs and part ID mappings, plus descriptions for all 325 recipe items. Generate one audio clip per JSON cue if you want event-driven narration later. Audio playback is not connected yet. The captions describe the virtual CAD build; no torque values or unmodelled service procedures are implied.

Edit the JSON caption and narration fields to author wording. `Tools/EngineModel/build_learning_content.py` regenerates the shipped content and spoken script from the reviewed recipe; rerunning it replaces manual content edits. Unknown parts in a custom recipe receive a basic fallback instruction and description, so new chapters need their own authored content for the same guidance quality.

## Mobile engine display

`Assets/Scenes/MobileEngineDisplay.unity` opens with the complete engine. Drag the custom timeline rail to smoothly assemble or disassemble all 325 recipe operations: 317 physical components and 8 completed carrier placements. Scrubbing is deterministic and follows prerequisites. The slider also accepts a mouse wheel, START / COMPLETE jump to the endpoints, and PLAY TIMELAPSE plays the full sequence over 90 seconds. Piston, sump, head and idler components build on their separate fixtures before their units move into the engine. Motor wiring is excluded.

One finger rotates the model; two fingers move it in the camera plane; pinch zooms. In the Editor, use left mouse drag, middle mouse drag and the mouse wheel. Gestures beginning over a control stay with the UI. RESET VIEW restores orientation, position and zoom.

Tap a visible CAD surface to display its name, purpose and chapter in the bottom information card. The camera reserves a separate viewport above the dock so the information does not cover the engine. Scrubbing a selected component out of the sequence closes its details. The display scene contains visual meshes and exact selection colliders, with no player or gameplay rigidbodies.

To create the display scene in another project copy, use Tools → Engine Assembly → Create Mobile Engine Display Scene. Its builder creates native Unity assets and retains existing scenes. Keep MainMenu first in the build scene list.

## Validation

Native Unity verification passed 31 caption/display checks, 9 experience-menu checks and 73 assembly regressions. These cover dependency order, both timeline endpoints, reverse scrubbing, whole-unit piston motion, slider and scroll input, playback, exact-surface part selection, viewport separation, captions with hints disabled, grouped fasteners and scene routing. Portrait previews are saved in `Docs/Previews`. Android hardware performance and AR tracking are not part of these Editor checks.
