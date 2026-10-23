# Beginner lesson authoring

The current lesson source is `Tools/EngineModel/rewrite_learning_lessons.py`.
It preserves the reviewed 183 cue groups and all 325 recipe items. Piston and
valve repetitions share voice IDs; different components and fastener sizes keep
separate lessons. The legacy builder delegates to this source for version 3 data.

Each lesson gives the component's purpose, an identifying feature, and an action
using landmarks on the engine. Directions are relative to the engine, not the
camera or the floor. The scene's intake manifold is on positive local X, exhaust
housing on negative X, timing end on negative Z, and flywheel on positive Z.
The sump fastener locations and accessory spacer sides were checked against
the recipe target coordinates. These coordinates locate the authored model;
they are not dimensions or servicing specifications for a real engine.

The service tube and additional motor do not have a supplied engineering
specification. Their descriptions avoid inventing a fluid circuit or a specific
vehicle duty. No torque values, belt tensions, or timing settings are invented.

Function references consulted:

- [MAHLE piston ring functions](https://www.mahle-aftermarket.com/na/en/performance/piston-rings/)
- [MAHLE cylinder components](https://www.pl.mahle.com/en/products-and-services/passenger-cars/cylinder-components/)
- [Bosch common-rail system](https://www.bosch-mobility.com/en/solutions/powertrain/diesel/common-rail-system-piezo/)

Validation should distinguish content coverage from learning outcomes. Automated
checks establish that every part has a lesson and that the text fits the UI.
They cannot establish that a first-time learner can identify every mounting
point without hints. That requires a human play-through with hints disabled.

For a teacher play-through, useful comprehension questions are: how does a rod
turn the crankshaft; why does a piston need three different rings; how do springs
close the valves; why do idler bearings need two kinds of retaining ring; and how
does the timing belt differ from the accessory belt?

Revised WAVs are generated into `Assets/Audio/Narration/English/Assembly` and
`Repair` using the existing fixed acoustic reference. The completion job resumes
any changed lessons and validates text hashes, voice identity, WAV checksums,
sample format, duration and audible signal. Existing file paths and Unity GUIDs
are retained. See `Tools/Narration/beginner-job-status.json` for completion.
