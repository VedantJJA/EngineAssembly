# Section 0: controls and features

Choose **Section 0** in the chapter cards, or use its header action, to open the guide. The current build pauses and stays intact. Previous/Next changes lessons, Replay voice repeats a lesson, and Close guide returns to the same chapter. Narration uses the same voice and highlighted captions as the other lessons.

The guide selects controls from the running experience:

| Experience | Lessons |
| --- | --- |
| PC workshop | Movement, picking up matching parts one at a time, inventory, Easy/Medium/Hard placement, Q carry/placement, T move/rotate, lift levers, flip buttons, keyboard shortcuts, hints and chapters |
| Phone garage | Touch joystick and direct taps, matching pickups, lift dragging and flip buttons |
| AR / phone assembly | Surface placement when AR is active, ordered part cards, visible ghost targets, separate piston workspaces, one-finger rotation, two-finger movement, pinch zoom and help controls |
| Quest VR | Controller-ray UI, walking/snap turn/teleport, grip pickup, trigger placement, physical lift levers and flip buttons, pause and next-step assistance |
| Engine display | Timeline inspection and playback, engine motion, audio toggle and assembly speed |

Phone fallback omits the AR surface-scan lesson. The standalone mobile display omits assembly-card lessons. VR instructions are selected when the Quest session is active, including the shared PC scene.

Section zero is independent from the physical recipe chapters, so existing chapter numbers, JSON priorities and installed-part history stay valid.

Author text and control references in `Assets/Resources/WorkshopTutorial.json`. Voice exports are in `Assets/Audio/Narration/English/Tutorial`. Regenerate and align with the existing scripts in `Tools/Narration` after editing spoken text.

The PC HUD shares the AR palette, rounded cards, typography and caption treatment. Hold **Alt** to click the header actions; **Tab** and **Escape** still open chapters and pause directly.

Phone repair stores every removed part in the bottom hotbar. Tap a visible part named by the current removal step; it leaves the model and its card is revealed in the hotbar. Select the green replacement or a reusable part when the reassembly instruction requests it, then tap its visible ghost. The red broken bearing remains in the hotbar after the engine is rebuilt. Leaving the repair chapter resets this practice inventory.
