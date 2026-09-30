# Engine collision assets

The desktop and mobile workshop scenes use CoACD convex hulls on each of the 318 physical engine components. `Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab` is the reusable assembled model with its materials and collider hierarchy. Use a Unity prefab to retain these collision components; FBX does not store Unity MeshCollider settings.

Generated hull meshes live in `Assets/GeneratedColliders/EngineCoACD`. Identical source geometry shares assets. Parts, bearing components and split caps remain separate; generating colliders does not combine their rendered meshes or alter assembly order. Old broad colliders are disabled and replaced by children named `Generated Colliders (CoACD)`.

The normal bake requests a concavity threshold of 0.04, up to 24 convex hulls per mesh, and at most 64 vertices per hull. Housings that exceed the 15-minute search limit use `retry_engine.py`: threshold 0.1 with a bounded search. CoACD may exceed the requested concavity when the hull limit is reached, so these remain collision approximations. Picking and ghost line-of-sight use the visible CAD meshes independently of these hulls. Android performance still needs verification on the target device.

The original `GSwirl_Wires` source contains 36 vertices and edges but **no faces**. Its FBX mesh therefore had no usable Unity surface. `Tools/EngineModel/build_wire_surface.py` creates a thin visual sleeve along the preserved routing in a separate FBX; `909_Motor_Wiring_Surface.asset` converts it into the original part's local frame. The original Blender file is untouched. The sleeve radius is a presentation choice, not a calibrated CAD measurement.

## Rebuilding

`EngineColliderBake.Export` writes a mesh manifest and CoACD requests into `Tools/Colliders/EngineBake`. Run `Tools/Colliders/bake_engine.py` with that folder as its argument and the Python installation configured in `Tools/Colliders/settings.local.json`. The process resumes from completed results. `EngineColliderBake.Apply` imports the hulls, updates both workshop scenes and exports the prefab; run it in the isolated batch project to preserve open scene edits, then copy the generated assets and their `.meta` files together.

For individual scene changes, use **Tools → Engine Assembly → Batch Setup Parts**, select the desired roots or **Select all**, and choose **CoACD**. Retain the original CAD sources and recipe IDs.

## Interaction corrections

The PC crosshair is anchored to the exact screen centre at every aspect ratio. Pickup queries test visible part meshes in a separate physics scene, ignoring the player's own controller while respecting solid environment occlusion. Standing and crouching use the same selection logic. **M** toggles Move mode, with a `MOVE MODE ACTIVE` badge in the top-right corner.
