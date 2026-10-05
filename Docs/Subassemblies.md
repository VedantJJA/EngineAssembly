# Assembly process and subassemblies

The authoritative sequence is `Assets/AssemblyRecipes/three-cylinder.json` (schema version 2). Both desktop and mobile use this same recipe and AssemblyManager. The original CAD model retains its stable IDs. Gameplay includes 317 physical components and eight runtime carriers, for 325 recipe items. The motor wiring is excluded from assembly, captions and engine display; its source CAD asset remains available.

## Corrected process

| Section | Enforced sequence |
| --- | --- |
| Block/crank | Block → upper bearing shells/needle bearings → crankshaft |
| Pistons | Build each piston-and-rod unit away from the engine: piston → connecting rod → wrist pin → both circlips → rod studs → oil ring → second ring → top ring. After all three units are complete, install them upright. Turn the block over, fit the separate big-end caps, then the nuts. |
| Lower crankcase | Lower shells → bedplate → bedplate fasteners. On a separate upright fixture: sump → baffle → four baffle screws. Install the complete sump unit on the inverted engine → sump fasteners → turn upright → flywheel → retaining plate → retaining bolts. |
| Head | Flip the loose head upside down → insert all 12 valves from the chamber side → flip the head upright → for each valve: spring seat, stem seal, spring, retainer, both split keeper halves → install the completed head → head bolts |
| Timing | Cam bearings/shafts → front cover → camshaft keys → hubs → inner camshaft washers → hub bolts → crankshaft key → outer timing pulleys → crankshaft washer → outer fasteners → idler mounting plate/shafts → both inner shaft circlips. Build each idler pulley with its bearings and bore clips away from the engine, mount the units, then fit the outer shaft clips. Timing belt last. |
| Cover/fuel | Cover and its fasteners → injectors → rail and rail fasteners → return line → breather and fasteners → hose |
| Turbo/exhaust | Housing/support fasteners → bearings → shaft → shaft clips → wheels/M4 retainers → outlet housing → outlet band clamp → M8 clamp bolt |
| Intake | Manifold → fittings → external tube |
| Accessories | Upper bracket → lower bracket → spacers → alternator/starter/pulley → idler shaft → inner shaft circlip → build and mount accessory idler → outer shaft circlip → motor and mounting hardware → belt |

Equal-priority fasteners within each fastening operation remain unordered peers. Fasteners securing different layers do not share one global end-of-chapter priority: the baffle screws precede installation of the complete sump unit, and rod studs precede the separate cap nuts.

This is a CAD-supported educational process, not a verified OEM workshop procedure. The source is a converted SLDASM with no engine manufacturer/BOM/service procedure. The geometry/contact sheets, original object identities and installed positions were reviewed; inferred accessory labels remain provisional. No missing gaskets, seals, bearing balls, torque values, timing settings or tooling operations were invented. Generic piston terminology is consistent with [Kubota's piston-pin definition](https://discovery.engine.kubota.com/dictionary/135/); this does not identify the supplied engine as a Kubota engine.

## Gameplay

The head's 12 valve stacks have individual dependency chains matched from their CAD target positions, rather than suffix numbering (the imported component numbering differs between families). `ModelReview/valve-stacks.json` records each valve and its seat/seal/spring/retainer/keeper pair. The upright spring stacks share a priority so you can finish one stack before starting another. All valves must be inserted first. F flips the active bench workpiece; orientation checks use that workpiece's frame, independently of the engine block. The fitted spring and retainer poses represent the compressed installation state; no compressor tool has been added. Run `Tools/EngineModel/revise_head_valves.py` after regenerating the base recipe with `revise_recipe.py`.

- Order checks apply to picking up a part in Build mode and to installing it, on PC and mobile and at every difficulty.
- A carrier cannot be picked up for installation until every member is built. Picking a member of a completed unit on PC selects the whole unit. It moves with every original member intact.
- A complete detached unit uses one dynamic carrier body and handling collider. Member bodies and colliders are suppressed while the unit is carried or dropped, preventing self-collision and sudden launches. Installation restores the original member colliders, including precise CAD cavity surfaces, and disables the carrier proxy.
- A rod cap never joins the piston carrier; it is installed separately after the piston unit reaches the crankshaft.
- Picking up a PC base/platform carries that fixture without a global Move mode; installed units follow their parent engine. F uses the next operation's destination, so a completed piston/head unit no longer makes the loose fixture flip when the engine should turn. Complete units can be carried before the destination is correctly oriented, but installation still enforces that orientation.
- Mobile gives every loose subassembly its own workspace and focuses one at a time, then returns to the engine for unit installation. Each piston has an independent transform; other fixtures and their cards are hidden. Inactive future-chapter parts are included when caching renderers so visibility remains correct after chapter changes.
- One finger rotates the active model. Two fingers translate it parallel to the camera image plane. Transitioning from two contacts back to one waits for all fingers to lift, preventing unintended rotation or placement. Gestures begun on cards stay with the UI. Middle-mouse drag emulates pan in the mobile scene.
- Hints start enabled and render through geometry. H toggles hints on PC. Seeing an overlay does not authorize placement: Easy clicks use exact-mesh visibility rays; PC Hard/Medium snapping also requires a visible target surface.
- Reverse disassembly removes mounting hardware and complete units before their internal members. Selecting another chapter reconstructs prior subassemblies at their correct engine poses.

## Recipe representation

`subAssemblies` contains each carrier's stable ID/rootPartId, previewPartId, benchPosition, focusCenter and focusRadius. The carrier also has its own PartDefinition describing its final engine socket and prerequisites for **all** members.

Each member's `subAssemblyId` identifies its carrier. Its target pose is relative to that carrier; it does not depend on the carrier being installed in the engine. Ordinary part dependencies and priorities still govern the bench build. This avoids a cyclic dependency between a unit and the parts required to make it.

AssemblyManager creates missing carriers at runtime. Original model objects, mesh assets and script GUIDs are retained. Carrier BoxColliders support PC handling; ghost visibility uses the original exact meshes. Original CAD ID prefixes remain stable identifiers; the recipe's `order` field controls the revised sequence and the number shown on cards. Do not infer current gameplay order from an old filename prefix.

Version 1 recipes still load. Version 2 validation rejects missing/duplicate carriers, invalid member references, nested groups, a carrier before its members, missing member prerequisites, and dependency cycles. Saving or capturing the configured recipe preserves subassembly definitions.

## Review and verification

`ModelReview/three-cylinder-before-subassemblies.json` preserves the prior recipe. `Tools/EngineModel/revise_recipe.py` creates the revised sequence, with the component coverage summary in `ModelReview/assembly-order-review.json`. `MobileAssemblyBuilder.GenerateSubAssemblyArtworkBatch` renders complete-unit card thumbnails and measures fixture framing from assembled mesh bounds.

`SubAssemblyVerification.RunBatch` exercises the revised native Unity scene: all chapters, piston builds at all three difficulties, original CAD-pose preservation, order rejection, whole-unit PC pickup/movement, reverse disassembly, default X-ray materials, occluded placement rejection, and injected one-/two-contact touch events. Device performance and physical Android touch/AR tracking still require device testing.

`Tools/EngineModel/revise_retainer_order.py` updates only the piston, timing, turbo and accessory chapters while preserving target geometry and unrelated chapter peer ordering. `SubAssemblyDropVerification.RunBatch` verifies enforced retainer order and repeated native-physics pickup/drop cycles for all eight units.

