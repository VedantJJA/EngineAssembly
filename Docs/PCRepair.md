# PC bonus chapter: Repair

Choose **Repair** in the Tab chapter-card grid. The engine starts completely assembled. Captions and optional hints guide a timing-idler bearing replacement; the rest of the engine remains assembled.

| Step | Operation |
| --- | --- |
| 1 | Remove the timing belt; press G to set it aside. |
| 2 | Remove idler 1's outer shaft circlip. Keep its inner shaft circlip fitted. |
| 3 | Remove the complete idler; G stages it on the small movable platform. |
| 4–5 | Remove both bearing-bore circlips from the loose idler. |
| 6 | Remove the outer bearing and keep it for reuse. |
| 7 | Remove and retire the faulty inner bearing. |
| 8 | Pick up the new bearing from the parts table and fit it to the vacant inner bearing socket. |
| 9 | Refit the original outer bearing. |
| 10–11 | Refit both bore circlips. |
| 12 | Install the rebuilt idler on its shaft. |
| 13 | Refit the outer shaft circlip. |
| 14 | Refit the timing belt. |

Removal advances after the part is set aside, not while it is held. The large table stays locked and the small platform remains movable. Removed parts are staged separately on the table; the original bearing is retired after removal. Reassembly keeps the normal Easy/Medium/Hard snap rules and line-of-sight checks. Captions remain usable with hints disabled.

`PCRepairChapter` adds the bonus at runtime when the PC workshop UI starts. The physical replacement is a clone of `510_Idler_Ball_Bearing_6203_01`, with ID `Repair_Replacement_Timing_Idler_Bearing`. It occupies the original socket and becomes a member of the rebuilt carrier. Original CAD meshes and saved scenes are preserved. Leaving or restarting Repair removes the temporary clone and reconstructs the chosen chapter normally.

The shared assembly recipe still has 325 items and nine normal chapters. The bonus does not appear in mobile or exported recipe JSON. `Docs/RepairNarration.txt` provides the displayed instructions for later TTS.

Native verification is available through `RepairWorkspaceVerification.RunBatch` in the isolated verification project, with results in `Tools/Verification/repair-workspace-report.json`.

Verification passed 168 checks covering the full repair in Easy and Hard, visible-surface replacement pickup, preserved scale and unaffected CAD poses, cleanup of temporary objects/ghost queries, JSON export, the six-card viewport, and independent mobile piston workspaces. The complete mobile assembly/touch suite passed 73 checks and the PC menu/editing suite passed 24 checks.
