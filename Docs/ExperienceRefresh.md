# Engine Lab experience refresh

The experience menu lists the PC/phone workshop first, followed by AR and VR.
Each device tab offers assembly, repair and display. A phone automatically gets
landscape touch controls when opening the combined workshop. Quest devices use
the existing tracked PC workshop, lift controls and locomotion.

| Activity | Combined PC / phone workshop | AR | Quest VR |
|---|---|---|---|
| Assembly | Ordered chapters; PC flip requirements | Touch cards and anchored active build | PC chapters with controller interaction |
| Repair | Timing-idler bearing replacement chapter | Same removal/replacement sequence with visible-mesh taps | Same PC repair sequence |
| Display | Final chapter after Repair; select a visible component | Timeline display and spoken component information | Final PC display chapter with controller rays |

The shared UI uses blue accents with grey workshop surfaces. Experience text uses
ordinary punctuation without em dashes. The phone display builds each subassembly
in a separate area with the main engine and other units hidden. Completed units
rejoin the engine during their installation step. Playback chooses an unobstructed
wide viewing angle and turns the engine in tracked AR instead of moving the real
camera. Enclosed fittings use a temporary cutaway, labelled in the information
card. The complete view restores every part. Its Settings card slows animation from normal speed down to 0.1×;
it cannot fast-forward. Tracked AR
keeps the physical camera under the user's control. The Start, Complete and close
buttons marked in the reference were removed; the timeline still supports direct
scrubbing. Engine materials are used in the regenerated part thumbnails.

## Voice and captions

The catalogue contains 183 assembly steps covering all 325 recipe components,
with 112 shared assembly lessons and 112 distinct display descriptions. Together
with repair and companion cues this is 246 recordings. The smaller and larger
crankshaft bearings have separate lessons. Display descriptions explain function;
assembly lessons also explain fitting. Repeated piston/valve lessons stay silent
after their first occurrence. Recordings do not open with This, These or Those.
Collecting matching parts does not trigger a voice announcement. The retired
recording is archived outside Assets when generation completes.

Any of the three pistons may be built first. After its first component is fitted,
that unit must be completed before starting another. The order inside each piston
remains enforced, and matching clips and studs retain equal priority. Phone cards
offer the unstarted pistons before showing the selected unit's remaining parts.

PC initial bearings and the crankshaft now require the underside orientation.
Flip announcements queue behind the current lesson. Successive recordings have
a 0.75-second gap, with a longer gap after a flip announcement. Captions show a
rolling phrase with spoken words dark and upcoming words grey. The background
alignment process derives word timing from the actual WAV; timing is estimated
until an aligned recording is available.

Audio is generated using the fixed local Qwen acoustic reference. Current WAVs
are imported and connected automatically when Unity is in Edit mode, and after
leaving Play mode. Audio folders are:

- `Assets/Audio/Narration/English/Assembly`
- `Assets/Audio/Narration/English/Repair`
- `Assets/Audio/Narration/English/Display`

Generation is resumable by waveform/text checksums. `Tools/Narration/progress.json`
records counts. `Tools/Narration/experience-export-status.json` records the queued
completion job: it waits for audio, audits every cue, finishes word alignment,
then builds and verifies fresh phone and Quest APKs. Existing APK files are from
the previous build until this status says complete. This is a one-shot local
process, not a recurring automation; the computer must remain running.

## Verification limits

The isolated Unity tests exercise the menu, voice coverage, queued flips, live
captions, the PC display chapter, isometric playback/slow motion, the complete touch repair,
VR scene/UI binding and AR fallback. The tracked provider used in Editor tests
is a test double; it is never included in the player. Real Vuforia tracking,
Quest controller ergonomics and performance on the Galaxy A9 require hardware.
The reports are in `Tools/Verification/experience-refresh-report.json`.
