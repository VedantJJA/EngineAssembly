using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    /// <summary>
    /// Captures the positions of unassembled parts and the player's position/orientation during Play Mode,
    /// and permanently writes them back to the Edit Mode scene when stopping Play Mode.
    /// Uses Unity's GlobalObjectId system to ensure duplicate names (e.g. "Cube") are matched uniquely and accurately.
    /// </summary>
    [InitializeOnLoad]
    public static class PartLayoutSaver
    {
        private const string SessionKey = "EngineAssembly_SavedPartLayout";

        [Serializable]
        public class PlayerData
        {
            public string globalObjectId;
            public bool shouldSave;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
        }

        [Serializable]
        public class PartData
        {
            public string globalObjectId;
            public string hierarchyPath;
            public string partName;
            public string partId;
            public bool saveTransform;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public bool hasRecordedIndex;
            public int orderIndex;
            public int localOrderIndex;
            public int groupIndex;
            public bool wasDisassembledDuringRecording;
            public int disassemblyStepNumber;
            public string disassemblyInfo;
        }

        [Serializable]
        public class DisassemblyStepData
        {
            public string partName;
            public string partId;
            public int assignedIndex;
            public int groupIndex;
            public int stepNumber;
            public bool isSubAssembly;
            public string subAssemblyName;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public float timestamp;
            public string description;
        }

        [Serializable]
        public class PartLayoutPackage
        {
            public PlayerData playerData = new PlayerData();
            public List<PartData> parts = new List<PartData>();
            public List<DisassemblyStepData> disassemblySteps = new List<DisassemblyStepData>();
        }

        static PartLayoutSaver()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            PlayerAssemblyController.OnSaveLayoutRequested -= SaveCurrentLayoutNow;
            PlayerAssemblyController.OnSaveLayoutRequested += SaveCurrentLayoutNow;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                CaptureData();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplyCapturedDataToScene();
            }
        }

        /// <summary>
        /// Manually captures and writes the current positions to the scene immediately (works in Play Mode or Edit Mode).
        /// </summary>
        public static void SaveCurrentLayoutNow()
        {
            if (EditorApplication.isPlaying)
            {
                CaptureData();
                Debug.Log("<color=cyan>[PartLayoutSaver] Captured current layout in Play Mode. It will be applied to the scene upon exiting Play Mode.</color>");
            }
            else
            {
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerAssemblyController>();
                if (player != null)
                {
                    EditorUtility.SetDirty(player.gameObject);
                    EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                    EditorSceneManager.SaveScene(player.gameObject.scene);
                    Debug.Log("<color=green>[PartLayoutSaver] Scene saved successfully in Edit Mode.</color>");
                }
            }
        }

        private static void CaptureData()
        {
            var player = PlayerAssemblyController.Instance;
            if (player == null)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerAssemblyController>();
            }

            bool savePositions = player != null ? player.SavePlacedPartsToSceneOnExit : true;
            bool savePlayer = player != null ? player.SavePlayerPositionOnExit : true;
            bool hasRecordedLog = player != null && player.RecordingLog != null && player.RecordingLog.Count > 0;

            PartLayoutPackage package = new PartLayoutPackage();

            // 1. Capture Player position & orientation
            if (player != null && savePlayer)
            {
                package.playerData = new PlayerData
                {
                    globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(player.gameObject).ToString(),
                    shouldSave = true,
                    worldPosition = player.transform.position,
                    worldRotation = player.transform.rotation
                };
            }

            // 2. Capture Parts
            AssemblyPart[] allParts = UnityEngine.Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include);

            HashSet<AssemblyPart> recordedParts = new HashSet<AssemblyPart>();
            if (hasRecordedLog)
            {
                foreach (var entry in player.RecordingLog)
                {
                    if (entry.part != null)
                    {
                        recordedParts.Add(entry.part);
                    }
                }
            }

            for (int i = 0; i < allParts.Length; i++)
            {
                AssemblyPart part = allParts[i];
                if (part == null) continue;

                bool shouldSaveTransform = savePositions && !part.IsSnapped;
                bool shouldSaveIndex = recordedParts.Contains(part);

                if (shouldSaveTransform || shouldSaveIndex)
                {
                    PartData data = new PartData
                    {
                        globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(part.gameObject).ToString(),
                        hierarchyPath = GetHierarchyPath(part.transform),
                        partName = part.gameObject.name,
                        partId = part.PartId,
                        saveTransform = shouldSaveTransform,
                        worldPosition = part.transform.position,
                        worldRotation = part.transform.rotation,
                        hasRecordedIndex = shouldSaveIndex,
                        orderIndex = part.OrderIndex,
                        localOrderIndex = part.LocalOrderIndex,
                        groupIndex = part.GroupIndex
                    };

                    // Attach rich disassembly recording state info if available
                    if (player != null && player.RecordedDisassemblyStates != null)
                    {
                        for (int r = 0; r < player.RecordedDisassemblyStates.Count; r++)
                        {
                            var rec = player.RecordedDisassemblyStates[r];
                            if (rec.part == part)
                            {
                                data.wasDisassembledDuringRecording = true;
                                data.disassemblyStepNumber = rec.stepNumber;
                                data.disassemblyInfo = rec.stateDescription;
                                break;
                            }
                        }
                    }

                    package.parts.Add(data);
                }
            }

            // 3. Capture Disassembly State Sequence History
            if (player != null && player.RecordedDisassemblyStates != null && player.RecordedDisassemblyStates.Count > 0)
            {
                foreach (var rec in player.RecordedDisassemblyStates)
                {
                    package.disassemblySteps.Add(new DisassemblyStepData
                    {
                        partName = rec.partDisplayName,
                        partId = rec.partId,
                        assignedIndex = rec.assignedIndex,
                        groupIndex = rec.groupIndex,
                        stepNumber = rec.stepNumber,
                        isSubAssembly = rec.isSubAssembly,
                        subAssemblyName = rec.subAssemblyName,
                        worldPosition = rec.worldPosition,
                        worldRotation = rec.worldRotation,
                        timestamp = rec.timestamp,
                        description = rec.stateDescription
                    });
                }
            }

            if (package.playerData.shouldSave || package.parts.Count > 0 || package.disassemblySteps.Count > 0)
            {
                string json = JsonUtility.ToJson(package);
                SessionState.SetString(SessionKey, json);
                Debug.Log($"<color=cyan>[PartLayoutSaver] Captured data from Play Mode: Player ({savePlayer}), {package.parts.Count} Parts (Positions: {savePositions}, Indices: {hasRecordedLog}), {package.disassemblySteps.Count} Disassembly Steps.</color>");
            }
        }

        private static void ApplyCapturedDataToScene()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            SessionState.EraseString(SessionKey);

            PartLayoutPackage package = JsonUtility.FromJson<PartLayoutPackage>(json);
            if (package == null) return;

            int appliedPositions = 0;
            int appliedIndices = 0;
            bool appliedPlayer = false;
            HashSet<UnityEngine.SceneManagement.Scene> affectedScenes = new HashSet<UnityEngine.SceneManagement.Scene>();

            // 1. Restore Player Transform
            if (package.playerData != null && package.playerData.shouldSave)
            {
                GameObject playerGo = null;

                // Try GlobalObjectId
                if (!string.IsNullOrEmpty(package.playerData.globalObjectId) && GlobalObjectId.TryParse(package.playerData.globalObjectId, out GlobalObjectId pId))
                {
                    var resolvedObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(pId);
                    if (resolvedObj is GameObject g) playerGo = g;
                    else if (resolvedObj is Component c) playerGo = c.gameObject;
                }

                // Fallback to PlayerAssemblyController in scene
                if (playerGo == null)
                {
                    var pComp = UnityEngine.Object.FindAnyObjectByType<PlayerAssemblyController>();
                    if (pComp != null) playerGo = pComp.gameObject;
                }

                if (playerGo != null)
                {
                    Undo.RecordObject(playerGo.transform, "Restore Player Position from Play Mode");
                    playerGo.transform.position = package.playerData.worldPosition;
                    playerGo.transform.rotation = package.playerData.worldRotation;

                    // If CharacterController is attached, sync its internal position
                    var cc = playerGo.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        Physics.SyncTransforms();
                    }

                    PrefabUtility.RecordPrefabInstancePropertyModifications(playerGo.transform);
                    EditorUtility.SetDirty(playerGo);
                    if (playerGo.scene.IsValid()) affectedScenes.Add(playerGo.scene);
                    appliedPlayer = true;
                }
            }

            // 2. Restore Parts
            AssemblyPart[] allSceneParts = UnityEngine.Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include);

            if (package.parts != null)
            {
                for (int i = 0; i < package.parts.Count; i++)
                {
                    PartData data = package.parts[i];

                    // 1. Match by GlobalObjectId (100% unique per GameObject in scene, immune to duplicate names like "Cube")
                    AssemblyPart match = null;
                    if (!string.IsNullOrEmpty(data.globalObjectId) && GlobalObjectId.TryParse(data.globalObjectId, out GlobalObjectId gId))
                    {
                        var resolvedObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gId);
                        if (resolvedObj is GameObject g) match = g.GetComponent<AssemblyPart>();
                        else if (resolvedObj is AssemblyPart ap) match = ap;
                    }

                    // 2. Match by hierarchy path
                    if (match == null && !string.IsNullOrEmpty(data.hierarchyPath))
                    {
                        for (int j = 0; j < allSceneParts.Length; j++)
                        {
                            if (allSceneParts[j] != null && GetHierarchyPath(allSceneParts[j].transform) == data.hierarchyPath)
                            {
                                match = allSceneParts[j];
                                break;
                            }
                        }
                    }

                    // 3. Match by unique PartId (only if exactly one match in scene)
                    if (match == null && !string.IsNullOrEmpty(data.partId))
                    {
                        AssemblyPart singleCandidate = null;
                        int candidateCount = 0;
                        for (int j = 0; j < allSceneParts.Length; j++)
                        {
                            if (allSceneParts[j] != null && allSceneParts[j].PartId == data.partId)
                            {
                                singleCandidate = allSceneParts[j];
                                candidateCount++;
                            }
                        }
                        if (candidateCount == 1)
                        {
                            match = singleCandidate;
                        }
                    }

                    // 4. Match by GameObject name
                    if (match == null && !string.IsNullOrEmpty(data.partName))
                    {
                        AssemblyPart singleCandidate = null;
                        int candidateCount = 0;
                        for (int j = 0; j < allSceneParts.Length; j++)
                        {
                            if (allSceneParts[j] != null && allSceneParts[j].gameObject.name == data.partName)
                            {
                                singleCandidate = allSceneParts[j];
                                candidateCount++;
                            }
                        }
                        if (candidateCount == 1)
                        {
                            match = singleCandidate;
                        }
                    }

                    if (match != null)
                    {
                        if (data.saveTransform)
                        {
                            Undo.RecordObject(match.transform, "Restore Part Transform from Play Mode");
                            match.transform.position = data.worldPosition;
                            match.transform.rotation = data.worldRotation;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(match.transform);
                            appliedPositions++;
                        }

                        if (data.hasRecordedIndex)
                        {
                            Undo.RecordObject(match, "Restore Part Order Indices from Play Mode");
                            match.OrderIndex = data.orderIndex;
                            match.LocalOrderIndex = data.localOrderIndex;
                            if (data.groupIndex > 0)
                            {
                                match.GroupIndex = data.groupIndex;
                            }
                            PrefabUtility.RecordPrefabInstancePropertyModifications(match);
                            appliedIndices++;
                        }

                        EditorUtility.SetDirty(match.gameObject);
                        EditorUtility.SetDirty(match);

                        if (match.gameObject.scene.IsValid())
                        {
                            affectedScenes.Add(match.gameObject.scene);
                        }
                    }
                }
            }

            foreach (var scene in affectedScenes)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (package.disassemblySteps != null && package.disassemblySteps.Count > 0)
            {
                Debug.Log($"<color=cyan>[PartLayoutSaver] Preserved {package.disassemblySteps.Count} disassembly state steps recorded in Play Mode:</color>");
                List<PlayerAssemblyController.DisassemblyRecordInfo> stepsToSave = new List<PlayerAssemblyController.DisassemblyRecordInfo>();

                for (int s = 0; s < package.disassemblySteps.Count; s++)
                {
                    var step = package.disassemblySteps[s];
                    Debug.Log($"  <color=orange>[Step {step.stepNumber}]</color> {step.description} (Time: {step.timestamp:F1}s)");

                    stepsToSave.Add(new PlayerAssemblyController.DisassemblyRecordInfo
                    {
                        stepNumber = step.stepNumber,
                        partDisplayName = step.partName,
                        partId = step.partId,
                        assignedIndex = step.assignedIndex,
                        groupIndex = step.groupIndex,
                        isSubAssembly = step.isSubAssembly,
                        subAssemblyName = step.subAssemblyName,
                        worldPosition = step.worldPosition,
                        worldRotation = step.worldRotation,
                        timestamp = step.timestamp,
                        stateDescription = step.description
                    });
                }

                // Ensure the persistent JSON file on disk is written/refreshed in Edit Mode
                PlayerAssemblyController.SaveDisassemblySequenceToFile(stepsToSave);
            }

            if (appliedPlayer || appliedPositions > 0 || appliedIndices > 0 || (package.disassemblySteps != null && package.disassemblySteps.Count > 0))
            {
                string playerStatus = appliedPlayer ? "Player Position & " : "";
                string stepsStatus = (package.disassemblySteps != null && package.disassemblySteps.Count > 0) ? $", {package.disassemblySteps.Count} Disassembly Steps" : "";
                Debug.Log($"<color=green>[PartLayoutSaver] Permanently saved back to scene: {playerStatus}{appliedPositions} Part Positions, {appliedIndices} Order Indices{stepsStatus}. Scene saved successfully!</color>");
            }
        }

        private static string GetHierarchyPath(Transform current)
        {
            if (current == null) return string.Empty;
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }
    }
}
