# EngineLab: 3-Cylinder Engine Assembly & Interactive Workshop

A cross-platform, high-fidelity mechanical engineering and educational simulation built in **Unity 6 (Universal Render Pipeline)**. The project features an authentic three-cylinder combustion engine simulation with **317 interactive physical components** across **102 part families**, **8 complete subassembly units**, real physics, convex collision meshes, and complete step-by-step voice-guided narration.

Supported deployment targets:
- **PC Desktop (Windows Standalone)**: First-person workshop with full physics, lift stations, tray supply management, and chapter assembly.
- **Meta Quest VR (OpenXR)**: Room-scale 6DoF VR with physical hydraulic lift levers, hatch flip cycles, teleportation, direct controller gripping, hand visualization, and wrist menus.
- **Mobile AR (Android / iOS)**: Vuforia Ground Plane AR tracking with surface anchoring, gesture manipulation, and step-by-step part installation.
- **Mobile Touch Fallback**: Dedicated touch experience with card-based progression, gesture orbit/pan/zoom, and assembly previews for non-AR mobile devices.

---

## Table of Contents
- [Prerequisites](#prerequisites)
- [Project Setup](#project-setup)
- [Scenes & Experiences](#scenes--experiences)
- [Controls Reference](#controls-reference)
- [Subsystems & Tools](#subsystems--tools)
  - [Convex Collider Decomposition (Tools/Colliders)](#convex-collider-decomposition)
  - [Voice Narration Pipeline (Tools/Narration)](#voice-narration-pipeline)
  - [Automated Verification (Tools/Verification)](#automated-verification)
- [Building the Project](#building-the-project)
- [Repository Structure](#repository-structure)
- [Troubleshooting & FAQs](#troubleshooting--faqs)

---

## Prerequisites

| Requirement | Recommended Version | Notes |
|-------------|---------------------|-------|
| **Unity Editor** | `6000.5.3f1` (Unity 6) | Universal Render Pipeline (URP). Install via [Unity Hub](https://unity.com/download). |
| **Git LFS** | Latest | **Mandatory** for 3D models (`.blend`, `.fbx`), audio (`.wav`), and textures (`.exr`, `.png`). |
| **Unity Modules** | Android Build Support (NDK & OpenJDK) | Required if building for Android Phone AR or Meta Quest. |
| **XR Runtime** | Oculus / Meta Quest Link / SteamVR | For OpenXR PC testing with a VR headset. |
| **Python** | 3.10 – 3.12 (64-bit) | Optional, only needed for local collider baking or regenerating narration audio. |

---

## Project Setup

### 1. Clone the Repository with Git LFS
Ensure Git LFS is installed and initialized before cloning:
```bash
# Initialize Git LFS on your machine
git lfs install

# Clone the repository
git clone https://github.com/VedantJJA/EngineAssembly.git
cd EngineAssembly

# Pull all large asset files (3D models, textures, audio)
git lfs pull
```

> [!IMPORTANT]
> If 3D models or narration `.wav` files appear as small text files (few bytes), Git LFS was not initialized. Run `git lfs pull` in the repository root to download the binary contents.

### 2. Open in Unity Hub
1. Launch **Unity Hub**.
2. Click **Add > Add project from disk**.
3. Select the `EngineAssembly` repository folder.
4. Set the Editor Version to **6000.5.3f1**.
5. Open the project. Unity will compile scripts and generate the local `Library/` asset cache.

---

## Scenes & Experiences

All main scenes are located under `Assets/Scenes/`:

| Scene | Target Platform | Description |
|-------|-----------------|-------------|
| `MainMenu.unity` | Universal Hub | The main menu launcher providing access to Assembly, Repair, Learning, and Platform scenes. |
| `AssemblyWorkshop.unity` | PC Desktop | Full first-person interactive garage workshop with 317 parts, parts supply tables, movable platforms, and inspection mode. |
| `RepairVRFallback.unity` | Meta Quest / OpenXR / PC | Room-scale VR workshop featuring hydraulic lift stations, subassembly benches, physical lever/flip mechanics, and direct hand tracking. |
| `MobileAssemblyAR.unity` | Mobile Android / iOS | Vuforia Ground Plane AR mode. Detects flat ground, anchors the engine, and guides touch assembly. |
| `MobileDisplayAR.unity` | Mobile Android / iOS | AR inspection mode with 4-stroke cycle animation, section views, and exploded component views. |
| `MobileAssemblyARFallback.unity` | Mobile Touch | Touch fallback for non-AR mobile devices with card-based assembly and orbital gesture controls. |
| `MobileDisplayARFallback.unity` | Mobile Touch | Touch fallback for non-AR engine inspection, stroke timing, and operating diagnostics. |

---

## Controls Reference

### PC Desktop Controls (`AssemblyWorkshop.unity`)
- **Move**: `W`, `A`, `S`, `D`
- **Look**: Mouse
- **Jump / Crouch**: `Space` / `C`
- **Grab / Pick up**: Left-click on part or platform handle
- **Place / Fit Part**: Left-click on glowing target ghost
- **Rotate Held Part**: Right-click + drag
- **Examine Mode**: `E` or `Q`
- **Flip Engine**: Aim at lift console and click the red flip button (or press `F`)
- **Lift Height**: Hold left-click on the hydraulic lever and move mouse vertically
- **Chapter / Pause Menu**: `Tab` or `Esc`

### Meta Quest VR Controls (`RepairVRFallback.unity`)
- **Smooth Locomotion**: Left thumbstick
- **Snap Turn**: Right thumbstick (Left/Right, 30° increments)
- **Teleport**: Right thumbstick (Hold Up to aim arc, release to land)
- **Direct Grab**: Grip button (controller or pinch gesture with Quest Hand Tracking)
- **Install / Snap**: Move part into ghost and press Trigger
- **Hydraulic Lift Lever**: Physically grip red lever and pull up/down
- **Flip Hatch Sequence**: Press red flip button on pedestal (opens hatch, lowers, rotates 180°, raises)
- **Wrist Menu**: Left controller menu button (or glance at left wrist in hand tracking)

### Mobile & AR Gestures (`MobileAssemblyAR.unity` / `MobileAssemblyARFallback.unity`)
- **1-Finger Drag**: Orbit/rotate around the active workpiece
- **2-Finger Drag**: Pan parallel to the camera view
- **Pinch**: Zoom in / out
- **Part Cards**: Tap card at bottom tray to select part
- **Place**: Tap glowing ghost in 3D viewport (or tap **Next Step** for immediate fit)
- **Flip Button**: Invert workpiece 180° for upside-down assembly steps (e.g. oil sump)
- **Reposition (AR)**: Re-scan plane to place engine anchor in a new location

---

## Subsystems & Tools

### Convex Collider Decomposition
Located in `Tools/Colliders/`. Generates optimized convex collision hulls for complex engine parts (such as the cylinder head and block) using **CoACD** and **V-HACD**.

1. Optional dependencies installation:
   ```bash
   python -m pip install --target Tools/Colliders/python_deps -r Tools/Colliders/requirements.txt
   ```
2. In Unity Editor, open:
   **Tools > Engine Assembly > Batch Setup Parts**
3. Select meshes in the scene and choose **CoACD** or **VHACD** for decomposition. Cooked collision meshes are saved in `Assets/GeneratedColliders/`.

### Voice Narration Pipeline
Located in `Tools/Narration/`. Synthesizes educational voiceovers using AI TTS (Qwen3-TTS) and alignment (Faster-Whisper).
- Shipped audio clips live in `Assets/Audio/Narration/English/`.
- Text transcripts and chapter cues live in `Assets/Resources/NarrationText.json` and `EngineOperationNarration.json`.
- Voice iteration history and source vocal templates are preserved in `Backups/Narration/`.

### Automated Verification
Located in `Tools/Verification/`. Contains automated headless verification suites to ensure engine socket placement, physics bounds, and chapter flow regressions are caught early:
- In Unity Editor: **Tools > Engine Assembly > Run Verification**
- Command-line compilation test: `Tools/Verification/compile.ps1`

---

## Building the Project

### Building for Windows PC
1. In Unity, go to **File > Build Settings...**
2. Platform: **Windows, Mac, Linux** > **Target Platform: Windows (x86_64)**
3. Ensure the required scenes (`MainMenu`, `AssemblyWorkshop`, `RepairVRFallback`) are checked.
4. Click **Build** and choose an output directory.

### Building for Meta Quest (OpenXR APK)
1. In **Build Settings**, switch platform to **Android**.
2. Texture Compression: **ASTC**.
3. Go to **Project Settings > XR Plug-in Management > Android tab**:
   - Check **OpenXR** (Feature group: **Meta Quest Support**).
4. Build the APK. Deploy to Quest via Meta Quest Developer Hub or ADB:
   ```bash
   adb install -r EngineAssembly-Quest.apk
   ```

### Building for Android Mobile AR (Vuforia)
1. Ensure **Vuforia Engine** is configured under **Project Settings > Vuforia Engine configuration**.
2. Add your Vuforia Developer App Key:
   **Tools > Engine Assembly > AR > Open Vuforia Licence Settings**
3. Switch platform to **Android** (IL2CPP, ARM64, Minimum API Level 29+).
4. Check `MainMenu`, `MobileAssemblyAR`, `MobileDisplayAR`, and fallback scenes.
5. Click **Build**.

---

## Repository Structure

```
EngineAssembly/
├── Assets/                 # Unity project assets
│   ├── Art/                # Materials, textures, and shaders
│   ├── Audio/              # Voiceover narration and background music
│   ├── GeneratedColliders/ # Pre-baked CoACD and VHACD mesh collision assets
│   ├── Model/              # 3D models (Blender .blend and FBX files)
│   ├── Prefabs/            # Assembled engine and workshop prefabs
│   ├── Resources/          # Narration JSONs, performance profiles, fonts
│   ├── Scenes/             # Playable workshop, AR, VR, and mobile scenes
│   ├── Scripts/            # C# codebase (Assembly, Interaction, VR, Player, UI)
│   ├── Settings/           # Universal Render Pipeline (URP) quality assets
│   └── UI/                 # Mobile cards, icons, and HUD elements
├── Backups/                # Local voice archives and design assets (excluded from git)
│   └── Narration/          # Iteration backups of narration audio
├── Builds/                 # Local export directory for standalone executables and APKs
├── Docs/                   # In-depth architectural & platform documentation
├── Packages/               # Unity Package Manager manifest & package locks
├── ProjectSettings/        # Unity project input, physics, editor, and build settings
└── Tools/                  # Standalone toolchain
    ├── Android/            # Frame pacing and manifest dependencies
    ├── Colliders/          # CoACD / VHACD mesh decomposition scripts
    ├── EngineModel/        # Model hierarchy audits and recipe migration scripts
    ├── Narration/          # Voice synthesis scripts and chapter audio tools
    └── Verification/       # Batch regression runners and assertions
```

---

## Troubleshooting & FAQs

### Missing Models or Pink/Magenta Shaders
- **Pink Shaders**: This project uses Universal Render Pipeline (URP). If shaders appear pink, ensure the active pipeline asset under **Project Settings > Graphics** is set to `UniversalRenderPipelineGlobalSettings` or `Desktop_RPAsset`.
- **Missing Meshes**: Check if Git LFS pulled the files. Run `git lfs pull` from your terminal.

### Vuforia AR Ground Plane Not Tracking
- Ensure good lighting and a textured floor surface (pure white or reflective floors may fail tracking).
- If your device lacks ARCore / ARKit hardware support, the application will automatically fall back to `MobileAssemblyARFallback.unity`.

### Quest VR OpenXR Not Initializing
- In **Project Settings > XR Plug-in Management**, verify that **OpenXR** is active.
- For Quest Link on PC, ensure Meta Quest Link desktop app is set as the active OpenXR Runtime (**Settings > General > OpenXR Runtime**).
