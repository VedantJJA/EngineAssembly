# Sub-Assembly System & Workshop Edit Mode Guide

Welcome to the comprehensive guide for the **Engine Assembly Simulator**'s modular Sub-Assembly Architecture and Workshop Edit Mode.

---

## 1. Sub-Assembly Architecture

In real-world mechanical assemblies, engines are composed of modular sub-assemblies (e.g., *Piston Units*, *Cylinder Head with Valvetrain*, *Alternator*, *Turbocharger*). Each unit consists of a base component and dependent child parts.

```
Main Engine Block (Root)
 └── Piston Sub-Assembly (SubAssembly + AssemblyPart)
      ├── Piston Crown [Base Floating Anchor] (Order Priority: 3)
      │    ├── ConRod Socket (Parented to Piston Crown)
      │    └── WristPin Socket (Parented to Piston Crown)
      ├── Connecting Rod [Follows Gravity] (Order Priority: 2)
      └── Wrist Pin [Follows Gravity] (Order Priority: 1)
```

### Core Design Rules
1. **Parent Component**: The group container (or base assembly object) holds **both** `SubAssembly` and `AssemblyPart` components.
2. **Child Components**: Direct child parts hold **only** `AssemblyPart` (with `ParentSubAssembly` linked to the parent).
3. **Local Index Counter**: Each sub-assembly maintains its own local ordering system (`LocalOrderIndex`), independent of the engine block's global sequence.
4. **Base Floating Anchor**:
   - The first part in the sub-assembly (highest `LocalOrderIndex`) **floats in mid-air** (`useGravity = false, isKinematic = true`).
   - It acts as the workshop bench fixture for assembling the unit.
5. **Secondary Parts Follow Gravity**:
   - All other child parts follow gravity (`useGravity = true, isKinematic = false`).
   - They rest on the table or floor until picked up by the player.
6. **Dynamic Sockets**:
   - All child part sockets are **parented directly to the floating base part's transform** (`firstPart.transform`).
   - When the base part moves or floats in the workshop, all child sockets and translucent ghost previews track it in real time!
7. **Docking Rule**:
   - A sub-assembly can only dock into the main engine when **100% of its internal parts are assembled**.
   - When docked into the engine, internal parts are locked and cannot be stripped off until the whole sub-assembly is removed first.

---

## 2. 1-Click Setup Methods

### Method 1: Hierarchy Context Menu (Fastest)
1. Select the parent GameObject in the Unity **Hierarchy** (e.g., `Piston_SubAssembly`).
2. Right-click and choose **`Engine Assembly -> Setup Sub-Assembly`** (or from the top menu: **`Tools -> Engine Assembly -> Quick Setup Sub-Assembly from Selection`**).
3. The tool automatically:
   - Adds `SubAssembly` + `AssemblyPart` to the parent.
   - Adds `MeshCollider (Convex)`, `Rigidbody`, and `AssemblyPart` to all child meshes.
   - Generates sockets at current assembled positions.
   - Anchors child sockets to the base part.
   - Configures the base part to float and other child parts to follow gravity.
   - Sequentially numbers local indices.

### Method 2: Batch Setup Window
1. Open **`Tools -> Engine Assembly -> Batch Setup Parts`**.
2. Select your sub-assembly root in the Hierarchy.
3. Check **`Setup as Sub-Assembly Group`**.
4. Click **`Apply Batch Setup`**.

### Method 3: Component Inspector (Per-Part Fine Tuning)
1. Select any child part of a sub-assembly in the Hierarchy.
2. In the Inspector, the **Sub-Assembly Socket Workflow** box will automatically appear.
3. It displays the part's role:
   - *Base / First Part (Floats in air - workshop anchor)*
   - *Child Part (Follows gravity - socket tracks base part)*
4. Click **`[Generate Sub-Assembly Socket (Follow Base Part)]`** to configure or update its socket.
5. Use **`[Make This The Base / Floating Part]`** to change which part serves as the floating base.

---

## 3. Shift Key Disassembly Rule

> [!IMPORTANT]
> When a sub-assembly is fully assembled (`100% of internal parts snapped`), the player must **press and hold `Shift`** to start disassembling it.

### How It Works:
- **Prevents Accidental Disassembly**: When moving, inspecting, or picking up a completed unit, parts will not accidentally pop off.
- **Starting Only**: Once the first part has been detached (`IsFullyAssembled` becomes `false`), the sub-assembly is "broken open". **All remaining child parts can be disassembled normally without holding Shift.**
- **Automatic Reset**: As soon as all parts are snapped back together, the seal is restored, and Shift is required again for the next disassembly.

### Visual HUD Indicators:
| State | HUD Badge | HUD Action | Color | Description |
|:------|:----------|:-----------|:------|:------------|
| **Fully Assembled (No Shift)** | `[HOLD SHIFT]` | `SUB-ASSEMBLY ASSEMBLED` | Amber Gold | *"Hold [Shift] to start disassembling '\<Unit Name\>'"* |
| **Fully Assembled (Holding Shift)** | `[SHIFT + CLICK]` | `START DISASSEMBLY` | Bright Green | *"Breaks open sub-assembly '\<Unit Name\>'"* |
| **Partially Disassembled** | `[E / CLICK]` | `DISASSEMBLE` | Cyan | *"Unsnaps part and picks it up into hand"* |

---

## 4. Workshop Edit & Disassembly Recording Mode (`F6`)

Press **`F6`** or click the on-screen **`[▶ START RECORD (F6)]`** button at any time during Play Mode to toggle **Disassembly Recording Mode**.

### Key Features of Recording Mode:
1. **Complete Assembly Order Bypass**:
   - In Record Mode, **all previously configured assembly order indices, local sub-assembly indices, and prerequisite rules are 100% ignored**.
   - You can disassemble parts in whatever sequence you choose without any locks blocking you.
2. **Start & Stop Record UI Buttons**:
   - **In-Game HUD Button**: A large clickable button in the top-right corner (`[▶ START RECORD (F6)]` when inactive, pulsing red `[■ STOP RECORD (F6)]` when recording).
   - **Mouse Freeing**: Press **`Alt`**, **`Tab`**, or **`Esc`** at any time to unlock your mouse cursor so you can freely click the on-screen button.
   - **Inspector Buttons**: When selecting the `PlayerAssemblyController` GameObject during Play Mode, dedicated `[▶ Start Disassembly Recording Mode]` and `[■ Stop Disassembly Recording Mode & Save]` buttons are provided right in the Unity Inspector.
3. **Disassembly State Info Recording**:
   - Every unsnapped part captures rich disassembly state information:
     - **Step Number**: Sequential step count in the recorded session (Step #1, Step #2, etc.).
     - **Part Identity**: Display name and unique `PartId`.
     - **Sub-Assembly Affiliation**: Whether the part belongs to a sub-assembly, and its sub-assembly name.
     - **Assigned Order Index**: Main `OrderIndex` or sub-assembly `LocalOrderIndex`.
     - **Transform Data**: Exact world position and rotation where the part was disassembled.
     - **Timestamp**: Exact elapsed session time.
     - **State Description**: Human-readable summary (e.g. `Step #1: Disassembled 'Piston Crown' from Sub-Assembly 'Piston_Assembly' => LocalOrderIndex #1`).
4. **Sequence Index & Group Controls (Groups of Same Parts)**:
   - **`J` / `L`**: Change the current sequence index (`J` = Down `-1`, `L` = Up `+1`).
   - **`I` / `K`**: Change the group index within the current index (`I` = Up `+1`, `K` = Down `-1`).
   - **Handling Groups of Same Parts**: When you have multiple identical parts (e.g., 4 spark plugs, 8 cylinder head bolts, 16 valve springs) that belong to the same step, keep them on the **same Index and Group**. Parts with the exact same Order Index and Group Index share equal priority and never block each other during assembly or disassembly!
   - **Auto-Advance Toggle**: In the Inspector, you can toggle `Auto-Advance Index On Disassemble`. When disabled (default), all parts you disassemble remain in the current Index and Group until you manually advance them with `L` or `I`.
5. **Undo Recording Step (`Z` / `[↩ UNDO]` Button)**:
   - Made a mistake during recording or disassembled the wrong part? Press **`Z`** or click **`[↩ UNDO (Z)]`** on the HUD banner / Inspector.
   - The last recorded step is removed from the sequence, the previous index/group is restored, and the part is **automatically re-assembled back into its engine socket**!
6. **Auto Assemble for Disassembly Recording (`Y` / `[⚙ AUTO ASSEMBLE]` Button)**:
   - To record a clean disassembly sequence from scratch, the engine needs to be fully assembled first.
   - Press **`Y`** or click **`[⚙ AUTO ASSEMBLE]`** at any time. All engine components will instantly snap onto their sockets, reset recorded steps to Step #1, and get everything ready for you to record disassembly.
7. **Persistent Sequence & Scene Saving**:
   - **JSON Export**: The complete disassembly sequence (step, part name, global ID, order index, group index, sub-assembly, world position/rotation, timestamp) is automatically saved to `Assets/RecordedDisassemblySequence.json`.
   - **Scene Hierarchy Saving**: `PartLayoutSaver` captures and permanently updates the `OrderIndex` and `GroupIndex` fields directly on the GameObject components in your scene file (`.unity`), as well as any workbench repositioning and player transforms.
   - **Save Triggers**:
     - Automatically when you click **`[■ STOP RECORD]`** or press **`F6`**.
     - Automatically when exiting Play Mode.
     - Pressing **`U`** at any time.
     - On demand by clicking **`[💾 SAVE]`** on the HUD banner or in the Inspector.

---

## 5. Controls & Shortcuts Cheatsheet

### Normal Gameplay
| Shortcut | Action | Description |
|:---------|:-------|:------------|
| **`E`** or **`LMB`** | Pick Up / Disassemble | Interacts with the hovered part |
| **`LMB`** (near socket) | Snap Part | Snaps held part into targeted socket (or any unoccupied slot in its group) |
| **`Shift + LMB`** | Start Disassembly | Breaks open a fully assembled sub-assembly |
| **`U`** | Permanent Save | Saves sequence to JSON and updates scene layout immediately |
| **`Y`** | Auto-Assemble | Assembles all parts onto the engine |
| **`Q`** | Drop Part | Drops currently held part |
| **`R` / `T` / `Scroll`** | Rotate Part | Rotates held part in hand |
| **`RMB` (Hold)** | Orbit Camera | Rotates camera around character |
| **`C`** | Crawl / Crouch | Toggles low-stance crawl |
| **`N`** | Next Assembly Hint | Flashes the next part to assemble |
| **`B`** | Next Disassembly Hint | Flashes the next part to disassemble |
| **`F6`** / **Click Button** | Toggle Edit Mode | Enters/exits Workshop Disassembly Recording Mode |
| **`Alt` / `Tab` / `Esc`** | Free Mouse Cursor | Unlocks cursor to click on-screen buttons |

### Inside Workshop Edit & Recording Mode (`F6`)
| Shortcut | Action | Description |
|:---------|:-------|:------------|
| **`F6`** / **Click [■ STOP RECORD]** | Exit & Save | Exits record mode; permanently saves sequence to JSON & Scene |
| **`X`** / **Click [⚡ STEP]** | Toggle Auto-Advance | Toggles auto-incrementing assembly index on disassemble (ON/OFF) |
| **`Z`** / **Click [↩ UNDO]** | Undo Last Step | Reverts last recorded step & auto re-assembles the part into its socket |
| **`Y`** / **Click [⚙ AUTO ASSEMBLE]** | Auto Assemble All | Snaps all parts into engine so you can record disassembly from scratch |
| **`U`** | Permanent Save | Saves sequence to JSON and captures scene positions immediately |
| **`LMB` / `E`** | Record Disassembly | Disassembles part (ignores previous order) and records state info |
| **`J`** | Index Down (`-1`) | Decrements next order index (highlights all objects in selected group) |
| **`L`** | Index Up (`+1`) | Increments next order index (resets group to 1; highlights selected group) |
| **`I`** | Group Up (`+1`) | Increments group index on current index (highlights selected group) |
| **`K`** | Group Down (`-1`) | Decrements group index on current index (highlights selected group) |
| **Click [💾 SAVE]** | Save Sequence | Saves `Assets/RecordedDisassemblySequence.json` and updates scene |
| **`Alt` / `Tab`** | Free Mouse Cursor | Unlocks cursor to click buttons without exiting mode |
| **`Q`** | Place / Reposition | Drops part onto table; position persists to scene |
| **`R` / `T`** | Adjust Rotation | Orients part prior to placing |

---

## 6. Pro Tips & Troubleshooting

- **Batch Setup Workflow (Same Index, Different Groups)**:
  - When you run the batch setup tool (`Tools -> Engine Assembly -> Batch Setup Parts`) or click **`Set All Selected Parts to Same Index, Different Groups`**, **all parts receive the exact same Order Index (1)**, while being assigned **distinct Group Indices** (1, 2, 3...).
  - **Smart Group Clustering**: Identical parts (such as 4 spark plugs, 8 cylinder head bolts, or 16 valve springs sharing the same mesh or base name) automatically share the same `GroupIndex`, while unique parts (e.g., cylinder head, gasket, intake manifold) each receive their own unique group.
  - You can also uncheck **"Group Identical Parts by Name/Mesh"** if you want every single part to have a completely distinct group index.
- **Recording Assembly Order via Disassembly**:
  - Start Play Mode with all parts batch-set to Order Index 1.
  - Press **`F6`** (or click **`[▶ START RECORD]`**) to enter Disassembly Recording Mode.
  - As you disassemble parts one by one from the engine, each part's **`OrderIndex` is automatically stamped to the current sequence step**, while keeping its assigned group intact!
  - **Auto-Advance (⚡ STEP)**: Defaults to **ON**. After unsnapping each part, the next order index automatically increments (`1 -> 2 -> 3...`). Press **`X`** or click **`[⚡ STEP: ON/OFF (X)]`** on the HUD to toggle manual vs automatic index incrementing.
- **Smart Group Slot Highlighting**: When you pick up a part from a group, **only open, unoccupied sockets show ghost holograms and pulsing highlights**. Sockets that already contain a placed part from that group will never show ghost holograms or overlays!
- **Universal Group Snapping**: You can snap a held part into **any** unoccupied slot in its group.
- **Visual Group Inspection**: In Edit Mode, tapping **`J`/`L`** or **`I`/`K`** immediately pulses all parts belonging to that `(Index, Group)` in bright cyan so you instantly see what parts belong to that stage.
- **Permanent Save with `U`**: Pressing **`U`** at any point writes `Assets/RecordedDisassemblySequence.json` and stages your scene hierarchy updates without needing to pause or exit Play Mode.
- **Order Bypass**: You can pick up and unsnap any engine component in any sequence while in record mode. All previous order requirements are bypassed.
- **Duplicate Object Names**: The system uses `GlobalObjectId`, so having multiple objects named `"Cube"` or `"Bolt"` will never cause cross-talk or positioning bugs.
- **Floating Base Moving**: If you pick up the floating base part and carry it around the room, all other child part sockets move with it. When you drop it, it remains floating right where you left it.
- **Scene Saving**: If you moved parts or recorded order during Play Mode, they are safely written into the `.unity` scene asset upon exiting Play Mode.
