# Phone and Quest performance

The phone player targets 60 frames per second. The Quest player uses a 72 fps application target; the OpenXR compositor controls the headset's actual refresh and frame pacing. These targets are not measured device frame rates.

## Changes

- Placement queries keep cached exact CAD mesh colliders and renderer references. Query poses update only when changed. The private query scene is inactive when no part is selected.
- Target hit tests reuse raycast buffers. The world-occlusion query falls back to the full result set if its buffer fills, preserving blocked-target rejection.
- Assembly recommendations use a single scan instead of sorting and creating a list every frame. Chapter order, equal-priority parts, piston exclusivity, and reverse disassembly are retained.
- Engine motion and cutaways avoid repeated material/property writes. Ghost tint and pose writes also skip unchanged values.
- Display playback avoids moving every stationary part, repeating recipe lookups, and forcing physics synchronization on every animation frame. Manual scrubbing, new-step view planning, and taps still synchronize before physics queries.
- Quest caches input actions, event-system components, and canvas binding. Runtime canvas registration notifies the rig when another menu appears. Teleport previews reuse buffers.
- Precision surfaces change collider state only when needed. Captions cache tokenization, text references, step headings, and layout dimensions while preserving spoken-word highlighting.
- Phone and Quest APKs use non-development IL2CPP builds with Android frame pacing. Phone remains GLES3 without native VR pre-initialization; Quest remains Vulkan with single-pass instancing.

## Rendering profiles

`Assets/Resources/WorkshopPerformanceProfiles.asset` references both profiles. Android selects the correct profile automatically. The PC render pipeline is unchanged.

| Setting | Phone | Quest |
|---|---|---|
| URP render scale | 0.8 | 0.9 |
| HDR | Off | Off |
| MSAA | Off | 2x |
| Shadow distance | 12 m | 12 m |
| Additional vertex lights per object | 2 | 2 |
| Fixed foveation | None | 0.5, when supported |

Both profiles retain the SRP Batcher, exact engine geometry, and readable close-range targets. Reflection-probe blending/box projection, light cookies/layers, and lens flares are disabled on these profiles. The Quest runtime profile is explicitly included in URP's build-time shader requirements. Quest foveation uses the installed OpenXR SRP API and does not request eye tracking.

The default 50 Hz physics timestep is retained. Catch-up simulation is limited to 0.1 seconds after a loading or thermal stall.

## Recorded comparison

Baseline was captured before the runtime changes. Twenty warmed calls were sampled across real Editor frames per operation, using D3D11 and Mobile quality on this workstation. Values below are median CPU time for the named operation, not total frame time or device FPS. The allocation counter returned zero in this Editor environment and is not used to claim allocation-free execution.

| Operation | Before | After | Reduction |
|---|---:|---:|---:|
| Phone ghost refresh, late chapter | 2.6642 ms | 0.7504 ms | 71.8% |
| Next assembly step, late chapter | 0.1347 ms | 0.1021 ms | 24.2% |
| Phone timeline, within one part | 0.5461 ms | 0.3280 ms | 39.9% |
| Engine motion and section | 0.4306 ms | 0.0990 ms | 77.0% |
| Quest UI binding, unchanged scene | 0.1161 ms | 0.0004 ms | 99.7% |
| Quest ghost refresh | 0.1969 ms | 0.0091 ms | 95.4% |

Reports: `Tools/Verification/performance-baseline.json` and `performance-after.json`. Run `EngineAssembly.Editor.EnginePerformanceVerification.After` in the isolated verification project to repeat the current measurement. The automatic comparison also checks ghost alignment after gestures, inactive idle queries, unchanged chapter recommendations, profile references, and newly opened VR tutorial navigation.

## Device validation

No phone or headset was attached for this comparison. Confirm sustained performance using the exported release APKs on the Galaxy A9 and Quest 2, including a long valve chapter, late-chapter pickup, engine motion with cutaway, timeline playback, AR camera tracking, and VR teleport/UI interaction. Use a separate profiling build to distinguish CPU, GPU, and thermal limits, then compare the release build after warming the device. Budgets are 16.67 ms at 60 fps and 13.89 ms at 72 fps.

Unity's [URP profiling guidance](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp) and Meta's [performance workflow](https://developers.meta.com/vr/documentation/unity/po-perf-opt-mobile/) describe the on-device measurement process.

## Completed validation

- 3,974 checks passed across the targeted performance checks, valve line-of-sight suite, Quest lift/controller suite, experience/repair suite, and operating-engine suite.
- Both ARM64 IL2CPP release APKs were rebuilt and passed archive, signature, manifest, and native-library verification. The phone player retains optional AR and excludes native headset startup libraries.
- Google AndroidX Games Frame Pacing 2.1.2 is stored unchanged with its verified checksum and Apache 2.0 license under `Tools/Android`. The generated Gradle project resolves only that module from the project-local repository; other dependencies keep their usual repositories.
- Built runtime C# sources match the workspace sources. Actual phone/headset FPS remains unmeasured.
