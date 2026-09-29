# Three-cylinder engine: labeled Blender asset

Source: `Assets/Model/Edit/3cyl.blend`, originally a SolidWorks SLDASM converted to Blender. The source file is unchanged.

Deliverables:

- `Assets/Model/Edit/3cyl_Labeled.blend`: editable Blender master.
- `Assets/Model/Edit/3cyl_Labeled.fbx`: export containing the same mesh/empty hierarchy for Unity.
- `ModelReview/parts_manifest.csv`: every original CAD object mapped to its new name, chapter, priority, parent, and identification notes. Motor source rows map to one joined object.
- `ModelReview/validation.json`: geometry and FBX round-trip checks.
- `ModelReview/parts_contact_sheet.png`: isolated visual reference for original Part1 through Part59.
- `ModelReview/labeled_overview.png`: assembled model after reorganization.

## Hierarchy and chapters

The root is `000_Three_Cylinder_Engine`. Chapter containers and subgroup containers are Blender empties, so the organization is present in the exported transform hierarchy; it does not depend on Blender collections.

| Chapter | Container prefix | Contents |
| --- | --- | --- |
| 1 | 100 | Cylinder block, upper main bearings, crankshaft |
| 2 | 200 | Three cylinder subgroups: piston, rod, wrist pin, rings, clips, rod cap, studs and nuts |
| 3 | 300 | Lower main bearings, crankcase bedplate, sump plate, sump, flywheel |
| 4 | 400 | Cylinder head and three valve subgroups: seats, seals, valves, springs, retainers, keepers |
| 5 | 500 | Front cover, camshafts, bearings, pulley keys/hubs/pulleys, idlers, timing belt |
| 6 | 600 | Head cover, injectors, rail, return line, breather and hose |
| 7 | 700 | Turbo/manifold housing, rotor shaft/bearings/wheels, outlet clamp, exhaust canister |
| 8 | 800 | Intake manifold, fittings and external tube |
| 9 | 900 | Accessory brackets, alternator, starter, idler, combined GSwirl motor, wiring, drive belt |

The three-digit prefix on a mesh is its assembly priority, ascending within its chapter. Equal prefixes are intentionally unordered peers. The suffix identifies the instance and does not impose assembly order. Example: `201_Piston_Cyl01_01` and `201_Piston_Cyl02_01` have the same priority.

All bolts, screws, studs and nuts within a chapter share its `x90` priority. There is no artificial bolt-by-bolt ordering. Retaining clips and keys remain at the step where they belong. Chapter and subgroup containers are organizational nodes, not installable parts.

Cylinder 01 is the timing-pulley end (source world Y approximately -0.226), Cylinder 02 is central (-0.558), and Cylinder 03 is the flywheel end (-0.890). These are project labels, not verified manufacturer cylinder numbering.

For repair mode, reuse a `200_Cylinder_01`, `200_Cylinder_02`, or `200_Cylinder_03` subgroup. The subgroup contains separate serviceable piston/rod components and can be reused without duplicating their geometry in this master asset.

## Motor treatment

The 113 objects named `asdasdasdasdasdasd-1_*` under the original `GSwirl` assembly were joined into `908_Motor_Unit_GSwirl_01`. This includes the unit's internal pieces, casing and pulley. It is a single mesh object, with its original material assignments retained. External mounting bolts/nuts and `GSwirl_Wires` remain separate. The separate starter motor and alternator remain individual components.

The source names and belt-driven geometry do not establish the exact OEM function of GSwirl. It is labeled as the user-requested motor unit, without claiming it is the starter. Its identification is explicitly marked provisional.

## Identification and import notes

Labels were assigned from isolated geometry, installed locations and surviving CAD hardware names. No manufacturer BOM or service manual was supplied. A few accessory functions remain provisional: sump plate, breather housing/hose, fuel return line, exhaust canister, accessory brackets/spacers, intake fittings and external tube. Their notes are in the CSV and Blender object properties. Names should not be treated as verified OEM nomenclature.

The chapter/step sequence is a proposed educational assembly sequence. It does not describe workshop installation procedures, torque, engine timing, or every mechanical dependency. No missing gaskets, seals or other absent CAD parts have been invented.

Original geometry, placement, object origins, materials and scale were retained. Physical units were not calibrated; CAD conversion scale should be checked against a known dimension before an AR/VR real-size experience. The CAD mesh remains detailed (577,274 vertices, 1,154,334 faces); joining the motor does not reduce polygon count. Runtime mesh optimization is a later task.

Both assets are already inside the Unity project's Assets folder. Use one representation of the model in a scene, typically the FBX export. The hierarchy and three-digit names carry the organization; no runtime code is required to inspect it. Custom properties (`assembly_priority`, `chapter_order`, `cad_source_name`, `is_fastener`, `identification_note`) are included in Blender and the FBX, but no Unity code to interpret them has been added.

FBX was reimported into Blender to check all names, parents and vertex counts. A Unity scene or interaction implementation was not changed as part of this work.

## Reproduction

Model-preparation scripts are under `Tools/EngineModel`. Run `organize_engine.py` using Blender against the original source, not against the labeled output. `verify_export.py` reopens both source and output, checks the merged motor positions, renders the result, and reimports the FBX to check its hierarchy.
