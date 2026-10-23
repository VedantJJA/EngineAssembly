# Engine display and operating view

The phone display, its AR version, and the PC display chapter share the operating animation. Both displays offer an independent assembly timeline and engine-motion mode. Quest uses the PC display controls through the existing VR pointer.

## Controls

* Both displays have two tabs: **Info / Timeline** and **Engine Motion**. The play/pause control operates the selected tab. The phone remembers both cycle and timeline position; the PC remembers the timeline position when switching views.
* **Play timelapse** plays the assembly sequence with part descriptions only. Fitting commands belong to hands-on assembly. Each part starts near its seat, pauses while the camera moves closer, slides into place, and stays seated briefly before the next fitting. Fasteners take at least 2.6 seconds, other parts 3.8 seconds, and complete subassemblies 5 seconds at normal speed. The assembly-speed slider supports 0.1 to 4 times normal speed. With **Timelapse audio** enabled, a new description waits for the current narration; matching parts sharing a description keep moving. Turning audio off stops speech immediately and removes narration waits, including at higher speeds. Phone and PC remember the audio and speed preferences.
* **Play engine** runs a slow teaching animation of the complete engine. The same control pauses and resumes motion.
* **Explain engine cycle** on PC, or **Explain cycle** on phones, starts a separate spoken tour: how the mechanism works, intake, compression, power, and exhaust. Cylinder one moves through each stroke at the pace of its description. The play/pause control pauses both voice and motion during the tour. Changing tabs, scrubbing, or inspecting a part cancels it. Regular motion resumes after the tour. All five clips use the same locked reference voice and speech-aligned captions as the lessons. Exports are in `Assets/Audio/Narration/English/Operation`.
* **Section on/off** reveals the internal mechanism or restores the exterior. On phones, the timeline becomes a scrubber for the two crankshaft revolutions of the four-stroke cycle. The settings panel also has an engine movement speed slider.
* **Assembled view** on PC restores the complete engine and stops presentation playback. **Info / Timeline** restores the build timeline. The PC timeline changes only presentation poses; it does not change the player's saved assembly progress or edit the recipe.
* Selecting a part pauses the engine and presents its description. Phone gestures still rotate, translate, and zoom the model. In AR the tracked camera remains under device control; the section faces the viewer.
* The PC display releases the cursor for its controls and component selection. Hold the right mouse button to look around; movement keys still work. Assembly controls in the other chapters retain their existing behaviour.
* Descriptions and instructions use the assembly caption-card style with bold type, shadow, and speech-aligned word highlighting. The phone timeline's lower dock contains only mode, transport, and scrub controls.

## Visibility and meshes

The build sequence keeps earlier components in the active subassembly. Enclosing housings use screen-door transparency when they hide the current fitting, instead of being switched off. Other workspaces remain hidden until their own assembly stage.

The operating section clips outer castings at a world-space shader plane. Stationary fasteners on the opened side disappear completely, preventing small unsupported screw fragments at the cut. Both forward and depth passes use the same plane. Interior pistons, rings, rods, valves, and camshafts remain intact. Both camshaft pulleys retain their five perimeter screws, which follow the matching hub's pivot, phase, and rotation. No mesh asset or stored mesh vertex is edited. Visible cut surfaces have a muted steel edge treatment; this is a shader cutaway, not generated solid geometry or a new CAD section.

The turbo outlet band clamp and its retaining bolt stay hidden throughout engine motion, including with the section switched off. Stopping engine motion restores their original visibility for the assembly timeline and part inspection.

## Mechanical and teaching model

The crankshaft drives the piston and connecting-rod linkage using the joint positions measured in the supplied CAD. Rods pivot at the crank and stay attached to the wrist pins. Rings and wrist-pin clips follow the pistons. Rod caps, studs, and nuts follow the rods. Camshafts, hubs, pulleys, and their fasteners turn at half crankshaft speed. Fixed idler shafts and mounting plates stay stationary. Valve lift is sampled from the cam outline at each valve's axial station. Display-only tappets fill the CAD's contact gap and stay connected to the valve tips and cam lobes. Valve stems, retainers, and keepers move together, while springs compress about their fixed lower seats.

Three coloured gas volumes follow the space above the piston crowns: blue for intake, gold for compression, orange for combustion/power, and grey for exhaust. The caption identifies the stroke of cylinder one, and the readout labels the stroke of all three cylinders. Density follows actual chamber volume, and colours and combustion intensity blend through stroke boundaries. The compression volume shrinks as its piston rises and expands during the power stroke.

This is an illustrative four-stroke teaching animation, not an OEM timing or thermodynamic simulation. The source CAD has cylinders one and three at the same crank phase and cylinder two opposite them, while its cam lobes are spaced for a different crank layout. The crank geometry is preserved so connecting rods stay attached. A reversible vertex-shader pose adjusts cam lobe phase by cylinder in the operating view, keeping the visible lobe, tappet, valve, and stroke aligned. Shaft journals stay at their original positions. Switching back to the assembly view restores the original CAD pose. An evenly spaced firing order is not invented. The turbo rotation is illustrative and is not a calculated gas-driven speed.

Four-stroke reference: [NASA, internal combustion engine operation](https://www.grc.nasa.gov/WWW/K-12/BGP/engopt.html). The four processes span two crankshaft revolutions; the camshafts turn once during that cycle.

## Verification

The operating cutaway removes the intake pipe fitting, union and external service tube with their supporting housing. Exterior view and the assembly timeline restore them. Valve springs shorten along their own valve axis; the seated end stays fixed while the top follows valve travel. Compression is measured in engine coordinates so nested subassembly scales do not change the motion.

`EngineOperationVerification.RunBatch` checks every timeline step for retained subassembly parts, approach and seated pauses, close-up framing, three piston linkages over two revolutions, the 2:1 crank-to-cam ratio, cam/tappet contact throughout the cycle, fixed spring seats, unchanged meshes, hidden-side fasteners, compression and expansion, both play controls, bookmarks, full transform restoration, preserved PC assembly progress, and leaving the display chapter safely. It fails on runtime errors as well as failed assertions, and renders captions and the operating section for visual review.

Reports and screenshots are written under `Tools/Verification`. The existing `ExperienceRefreshVerification.RunBatch` covers assembly order, mobile/AR repair, VR display entry, and the shared menus.
