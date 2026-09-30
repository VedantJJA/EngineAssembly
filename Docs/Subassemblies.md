# Assembly process and subassemblies

The authoritative sequence is `Assets/AssemblyRecipes/three-cylinder.json` (schema version 2). Both desktop and mobile use this same recipe and AssemblyManager. The 318 original CAD components retain their identities and separation. Seven runtime carriers add seven installation steps, for 325 recipe steps. The edge-only wiring receives a separate renderable surface as documented in [CoACD collision assets](CoACDColliders.md).

## Corrected process

| Section | Enforced sequence |
| --- | --- |
| Block/crank | Block → upper bearing shells/needle bearings → crankshaft |
| Pistons | Build each piston-and-rod unit away from the engine: piston → first pin circlip → connecting rod → wrist pin → second circlip → rod studs → oil ring → second ring → top ring. After all three units are complete, install them upright. Turn the block over, fit the separate big-end caps, then the nuts. |
| Lower crankcase | Lower shells → bedplate → bedplate fasteners → baffle → baffle screws → sump → sump fasteners → turn upright → flywheel → retaining plate → retaining bolts |
| Head | Build head, valve seats/seals, valves, springs, retainers and separate split keepers as a loose head assembly → install the completed head → head bolts |
| Timing | Cam bearings/shafts → front cover → keys/hubs/retaining hardware and pulleys → idler mounting plate/shafts. Build each idler pulley with its bearings and bore clips away from the engine, mount the unit, then fit shaft clips. Timing belt last. |
| Cover/fuel | Cover and its fasteners → injectors → rail and rail fasteners → return line → breather and fasteners → hose |
| Turbo/exhaust | Housing/support fasteners → bearings → shaft → shaft clips → wheels/retainers → outlet housing → outlet band clamp |
| Intake | Manifold → fittings → external tube |
| Accessories | Brackets/spacers → alternator/starter/pulley → build and mount accessory idler → shaft clips → motor and mounting hardware → wiring → belt |

Equal-priority fasteners within each fastening operation remain unordered peers. Fasteners securing different layers no longer share one global end-of-chapter priority: for example, the baffle screws precede the sump, and rod studs precede the separate cap nuts.

This is a CAD-supported educational process, not a verified OEM workshop procedure. The source is a converted SLDASM with no engine manufacturer/BOM/service procedure. The geometry/contact sheets, original object identities and installed positions were reviewed; inferred accessory labels remain provisional. No missing gaskets, seals, bearing balls, torque values, timing settings or tooling operations were invented. Generic piston terminology is consistent with [Kubota's piston-pin definition](https://discovery.engine.kubota.com/dictionary/135/); this does not identify the supplied engine as a Kubota engine.

## Gameplay

- Order checks apply to picking up a part in Build mode and to installing it, on PC and mobile and at every difficulty.
- A carrier cannot be picked up for installation until every member is built. Picking a member of a completed unit on PC selects the whole unit. It moves with every original member intact.
- A rod cap never joins the piston carrier; it is installed separately after the piston unit reaches the crankshaft.
- In PC Move mode, a loose subassembly fixture moves independently from the engine. Installed units follow the engine.
- Mobile focuses the active loose subassembly during its build steps, then returns to the engine for unit installation. Parts from other inactive fixtures are hidden and cannot invisibly block placement.
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

