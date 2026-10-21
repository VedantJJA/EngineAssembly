# Quest 2 controls and lift stations

The repair card opens the existing `RepairVRFallback` scene. On a supported Quest
headset the OpenXR session replaces desktop input with a tracked head and two
controllers. Ordinary phone and Windows starts keep XR disabled. A Windows repair
launch can also try an installed OpenXR runtime for Quest Link, with desktop
fallback when a headset is unavailable.

Controls:

- Walk physically within the tracked play space.
- Left stick: smooth movement relative to your view, with workshop collision.
- Right stick left/right: 30-degree snap turning.
- Hold right stick up to aim a teleport arc; release to move to a clear floor.
  Walls, steep surfaces, and blocked landing spaces are rejected.
- Grip while pointing at a component: pick it up. Release grip: put it down.
  Removing and setting aside a repair part advances the corresponding repair step.
- Trigger: select a world-space UI card or fit the held part into its visible ghost.
- Left menu button: pause/continue. Thumbsticks also scroll lists when pointing
  at UI. A/X performs the next-step assistance action.
- Grip a nearby red lift lever and move your controller vertically. You can also
  point at it and hold trigger. Release the holding input to center and stop it.
  Tracking loss and pausing also release the lever.
- Touch the round red flip button, or point and press trigger: open hatch, lower,
  turn, raise, close hatch. A sustained touch cannot repeatedly trigger flipping.

Held parts keep their scale and have collision disabled against the player.
Controller tracking loss freezes a carried part at its last pose instead of
dropping it or advancing repair. The headset and hands are tracked using the
Oculus Touch OpenXR interaction profile; UI uses XR Interaction Toolkit tracked
graphic raycasters and the project's existing custom cards.

All compact control panels stay fixed on ground-mounted pedestals, angled toward
their access aisles. Each physical lever/button operates its own station without
changing the keyboard-selected lift. Bases are sized using the completed engine
or subassembly geometry, including parts still on the supply benches. Subassembly
stations have at least 0.72 m between their footprints and remain clear of the fixed
supply benches. Grey hatch doors hinge downward inside the base footprint before
travel into an unlit black recess. Only the workpiece moves; controls and lift bases
stay fixed. White bases mark the keyboard-selected station and grey mark inactive
stations. PC arrow-key and F shortcuts remain available. On PC, aim at a lever,
hold left click and move the mouse up/down; release to center. Click the adjacent
flip button for the same hatch sequence.
Ordinary lowering stops with the workpiece 16 cm above the floor, leaving room for
the deck and closed hatch. Underground travel occurs only during an intentional
flip. Subassembly decks support the complete planned unit, so adding a connecting
rod cannot push the support surface down into the floor. Pistons start crown-down.
The main working deck is 1.05 × 1.10 m; its separate hatch fits the completed engine.
Each subassembly has a nearby fixed supply tray for its own parts. The flywheel,
retaining plate and associated bolts can be installed with either engine orientation.
Both the shaft and red grip accept mouse/ray grabs; direct VR grips work on either.

`Tools → Engine Assembly → VR → Configure Quest 2 and lift art` creates XR
settings, a tiled treadplate material and the black recess material through the
Editor. OpenXR automatic startup stays off so Vuforia phone scenes retain their
own camera. The Quest build uses ARM64 IL2CPP, Vulkan and single-pass instancing.
Phone AR and Quest use separate APKs and identifiers.

Background music is the looping CC0 track **Contemplation** by Joth, sourced from
https://opengameart.org/content/contemplation-0. It streams from
`Assets/Resources/Music/Contemplation.mp3`, follows the existing sound-volume setting,
and ducks during narration. Source and licensing details are saved beside the audio.

Editor checks exercise lift operation and sequencing, room-scale rig setup,
collision/teleport validation and controller ray/trigger UI input. Physical Quest 2
tracking, comfort, rendering performance and actual headset deployment still need
on-device testing.
