using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    /// <summary>
    /// Editor window for batch setup of assembly parts.
    /// Provides toggleable operations:
    /// 1. Add MeshCollider + Convex to selected objects
    /// 2. Add AssemblyPart + create socket at current position
    /// 3. Create SubAssembly group from parent + children
    /// </summary>
    public class AssemblyBatchSetup : EditorWindow
    {
        private bool addMeshCollider = true;
        private bool setConvex = true;
        private bool addAssemblyPart = true;
        private bool createSocketAtPosition = true;
        private bool setupSubAssembly = false;
        private bool applyToChildrenRecursively = false;
        private int batchOrderIndex = 1;
        private int batchStartGroupIndex = 1;
        private bool groupIdenticalParts = true;

        [MenuItem("Tools/Engine Assembly/Batch Setup Parts", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<AssemblyBatchSetup>("Assembly Batch Setup");
            window.minSize = new Vector2(380, 440);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Assembly Batch Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select one or more GameObjects in the Hierarchy, then toggle the desired operations below and click Apply.\n\n" +
                "• Mesh Collider: Replaces existing colliders with MeshCollider (Convex).\n" +
                "• AssemblyPart: Adds the component and creates a socket at the current position.\n" +
                "• Sub-Assembly: Sets up a parent as a SubAssembly with children as parts.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // --- Toggle 1: Mesh Collider ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            addMeshCollider = EditorGUILayout.ToggleLeft(new GUIContent("Add Mesh Collider", "Replace existing colliders with MeshCollider"), addMeshCollider);
            if (addMeshCollider)
            {
                EditorGUI.indentLevel++;
                setConvex = EditorGUILayout.ToggleLeft("Set Convex", setConvex);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // --- Toggle 2: AssemblyPart + Socket ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            addAssemblyPart = EditorGUILayout.ToggleLeft(new GUIContent("Add AssemblyPart + Socket", "Adds AssemblyPart and creates a snap socket at the current position"), addAssemblyPart);
            if (addAssemblyPart)
            {
                EditorGUI.indentLevel++;
                createSocketAtPosition = EditorGUILayout.ToggleLeft("Create Socket at Current Position", createSocketAtPosition);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // --- Toggle 3: Sub-Assembly Group ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            setupSubAssembly = EditorGUILayout.ToggleLeft(new GUIContent("Setup as Sub-Assembly Group", "Adds SubAssembly + AssemblyPart to parent, AssemblyPart to children"), setupSubAssembly);
            if (setupSubAssembly)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Select exactly ONE parent object.\n" +
                    "Its direct children will get AssemblyPart components.\n" +
                    "The parent gets SubAssembly + AssemblyPart.", MessageType.None);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // --- Recursive option ---
            if (!setupSubAssembly)
            {
                applyToChildrenRecursively = EditorGUILayout.ToggleLeft(
                    new GUIContent("Apply to Children Recursively", "Also process child GameObjects of each selected object"),
                    applyToChildrenRecursively);
            }

            EditorGUILayout.Space(6);

            // --- Toggle 4: Shared Batch Index Settings ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Batch Index Settings (Same Index, Different Groups)", EditorStyles.boldLabel);
            batchOrderIndex = EditorGUILayout.IntField(new GUIContent("Default Order Index", "All batch-setup parts will receive this order index (e.g. 1)."), batchOrderIndex);
            groupIdenticalParts = EditorGUILayout.ToggleLeft(new GUIContent("Group Identical Parts by Name/Mesh", "When enabled, identical parts (e.g. 4 spark plugs, 8 bolts) share the same group index, while distinct parts get different group indices. If disabled, every part receives a unique sequential group index."), groupIdenticalParts);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(12);

            // --- Selection info ---
            int selectionCount = Selection.gameObjects.Length;
            EditorGUILayout.LabelField($"Selected Objects: {selectionCount}", EditorStyles.miniLabel);

            EditorGUI.BeginDisabledGroup(selectionCount == 0);
            if (GUILayout.Button("Apply Batch Setup", GUILayout.Height(36)))
            {
                ApplyBatchSetup();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // --- Quick utility buttons ---
            EditorGUILayout.LabelField("Quick Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Select All MeshRenderer Objects in Scene", GUILayout.Height(24)))
            {
                SelectAllMeshRenderers();
            }

            if (GUILayout.Button($"Set All Selected Parts to Same Index, Different Groups (Idx #{batchOrderIndex})", GUILayout.Height(26)))
            {
                SetSameIndexDifferentGroups();
            }
        }

        public static string GetPartGroupingKey(GameObject go)
        {
            if (go == null) return "unknown";
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && !string.IsNullOrEmpty(mf.sharedMesh.name))
            {
                return mf.sharedMesh.name.ToLowerInvariant();
            }
            string clean = System.Text.RegularExpressions.Regex.Replace(go.name, @"[\s_.]*[\(\[\#]?\d+[\)\]]?$", "").Trim();
            return string.IsNullOrEmpty(clean) ? go.name.ToLowerInvariant() : clean.ToLowerInvariant();
        }

        private void ApplyBatchSetup()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select one or more GameObjects in the Hierarchy.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Assembly Batch Setup");

            if (setupSubAssembly)
            {
                ApplySubAssemblySetup(selected);
            }
            else
            {
                List<GameObject> targets = new List<GameObject>();
                foreach (var go in selected)
                {
                    targets.Add(go);
                    if (applyToChildrenRecursively)
                    {
                        GatherChildrenRecursive(go.transform, targets);
                    }
                }

                Dictionary<string, int> groupMap = new Dictionary<string, int>();
                int nextGroup = batchStartGroupIndex;

                int processedCount = 0;
                foreach (var go in targets)
                {
                    bool didWork = false;

                    if (addMeshCollider)
                    {
                        didWork |= ApplyMeshCollider(go);
                    }

                    if (addAssemblyPart)
                    {
                        int assignedGroup;
                        if (groupIdenticalParts)
                        {
                            string key = GetPartGroupingKey(go);
                            if (!groupMap.TryGetValue(key, out assignedGroup))
                            {
                                assignedGroup = nextGroup++;
                                groupMap[key] = assignedGroup;
                            }
                        }
                        else
                        {
                            assignedGroup = nextGroup++;
                        }

                        didWork |= ApplyAssemblyPart(go, assignedGroup);
                    }

                    if (didWork) processedCount++;
                }

                int totalGroups = nextGroup - batchStartGroupIndex;
                Debug.Log($"<color=cyan>[AssemblyBatchSetup]</color> Processed {processedCount} objects into {totalGroups} distinct groups (Order Index: {batchOrderIndex}).");
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        private bool ApplyMeshCollider(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return false;
            }

            // Remove existing colliders
            Collider[] existing = go.GetComponents<Collider>();
            foreach (var col in existing)
            {
                Undo.DestroyObjectImmediate(col);
            }

            MeshCollider mc = Undo.AddComponent<MeshCollider>(go);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = setConvex;

            return true;
        }

        private bool ApplyAssemblyPart(GameObject go, int assignedGroupIndex = 1)
        {
            // Skip objects without visual mesh
            MeshFilter mf = go.GetComponent<MeshFilter>();
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null)
            {
                return false;
            }

            AssemblyPart part = go.GetComponent<AssemblyPart>();
            bool isNew = part == null;
            if (isNew)
            {
                part = Undo.AddComponent<AssemblyPart>(go);
            }

            // Assign batch-configured shared order index and distinct group index
            SerializedObject partSo = new SerializedObject(part);
            SerializedProperty orderProp = partSo.FindProperty("orderIndex");
            if (orderProp != null) orderProp.intValue = batchOrderIndex;
            SerializedProperty groupProp = partSo.FindProperty("groupIndex");
            if (groupProp != null) groupProp.intValue = assignedGroupIndex;
            partSo.ApplyModifiedProperties();

            // Add Rigidbody if not present
            if (go.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(go);
            }

            if (createSocketAtPosition && isNew)
            {
                CreateSocketForPart(part);
            }

            return true;
        }

        private void CreateSocketForPart(AssemblyPart part)
        {
            string socketName = $"{part.gameObject.name}_Socket";
            GameObject socketObj = new GameObject(socketName);
            socketObj.transform.position = part.transform.position;
            socketObj.transform.rotation = part.transform.rotation;

            // Parent socket to part's current parent if available
            if (part.transform.parent != null)
            {
                socketObj.transform.SetParent(part.transform.parent, true);
            }

            AssemblySocket socketComp = socketObj.AddComponent<AssemblySocket>();

            SerializedObject socketSo = new SerializedObject(socketComp);
            SerializedProperty targetPartProp = socketSo.FindProperty("targetPart");
            if (targetPartProp != null) targetPartProp.objectReferenceValue = part;
            SerializedProperty socketIdProp = socketSo.FindProperty("socketId");
            if (socketIdProp != null) socketIdProp.stringValue = part.PartId;
            socketSo.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(socketObj, $"Create {socketName}");

            SerializedObject partSo = new SerializedObject(part);
            SerializedProperty targetProp = partSo.FindProperty("targetSnapPoint");
            if (targetProp != null) targetProp.objectReferenceValue = socketObj.transform;
            SerializedProperty sockProp = partSo.FindProperty("targetSocket");
            if (sockProp != null) sockProp.objectReferenceValue = socketComp;
            partSo.ApplyModifiedProperties();
        }

        private void ApplySubAssemblySetup(GameObject[] selected)
        {
            if (selected.Length != 1)
            {
                EditorUtility.DisplayDialog("Sub-Assembly Setup",
                    "Please select exactly ONE parent GameObject for sub-assembly setup.\n" +
                    "Its direct children will become the sub-assembly parts.", "OK");
                return;
            }

            GameObject parent = selected[0];

            // Add SubAssembly to parent
            SubAssembly subAsm = parent.GetComponent<SubAssembly>();
            if (subAsm == null)
            {
                subAsm = Undo.AddComponent<SubAssembly>(parent);
            }

            // Add AssemblyPart to parent (root part)
            AssemblyPart rootPart = parent.GetComponent<AssemblyPart>();
            if (rootPart == null)
            {
                rootPart = Undo.AddComponent<AssemblyPart>(parent);
            }
            SerializedObject rootSo = new SerializedObject(rootPart);
            SerializedProperty rOrder = rootSo.FindProperty("orderIndex");
            if (rOrder != null) rOrder.intValue = batchOrderIndex;
            SerializedProperty rGroup = rootSo.FindProperty("groupIndex");
            if (rGroup != null) rGroup.intValue = 1;
            rootSo.ApplyModifiedProperties();

            // Add Rigidbody to parent if not present
            if (parent.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(parent);
            }

            // Process direct children: add AssemblyPart only with distinct group indices
            Dictionary<string, int> childGroupMap = new Dictionary<string, int>();
            int nextChildGroup = 1;

            int childCount = 0;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                GameObject childGo = child.gameObject;

                // Skip non-mesh children (e.g. empty containers, lights)
                if (childGo.GetComponent<MeshFilter>() == null && childGo.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }

                if (addMeshCollider)
                {
                    ApplyMeshCollider(childGo);
                }

                AssemblyPart childPart = childGo.GetComponent<AssemblyPart>();
                if (childPart == null)
                {
                    childPart = Undo.AddComponent<AssemblyPart>(childGo);
                }

                int assignedChildGroup;
                if (groupIdenticalParts)
                {
                    string key = GetPartGroupingKey(childGo);
                    if (!childGroupMap.TryGetValue(key, out assignedChildGroup))
                    {
                        assignedChildGroup = nextChildGroup++;
                        childGroupMap[key] = assignedChildGroup;
                    }
                }
                else
                {
                    assignedChildGroup = nextChildGroup++;
                }

                SerializedObject childSo = new SerializedObject(childPart);
                SerializedProperty cOrder = childSo.FindProperty("orderIndex");
                if (cOrder != null) cOrder.intValue = batchOrderIndex;
                SerializedProperty cGroup = childSo.FindProperty("groupIndex");
                if (cGroup != null) cGroup.intValue = assignedChildGroup;
                SerializedProperty cLocal = childSo.FindProperty("localOrderIndex");
                if (cLocal != null) cLocal.intValue = batchOrderIndex;
                childSo.ApplyModifiedProperties();

                if (childGo.GetComponent<Rigidbody>() == null)
                {
                    Undo.AddComponent<Rigidbody>(childGo);
                }

                if (createSocketAtPosition)
                {
                    // For sub-assembly children, parent sockets under the sub-assembly root
                    CreateSocketForPartUnderParent(childPart, parent.transform);
                }

                childCount++;
            }

            // Auto-gather children and link references
            Undo.RecordObject(subAsm, "Auto-Gather Sub-Assembly");
            subAsm.AutoGatherChildren();
            subAsm.ReparentChildSocketsToFirstPart();

            // Configure physics: first part floats, other child parts follow gravity
            AssemblyPart firstChild = subAsm.GetFirstPart();
            foreach (var cp in subAsm.ChildParts)
            {
                if (cp == null) continue;
                Rigidbody cprb = cp.GetComponent<Rigidbody>();
                if (cprb != null)
                {
                    Undo.RecordObject(cprb, "Configure Child Rigidbody");
                    if (cp == firstChild)
                    {
                        cprb.useGravity = false;
                        cprb.isKinematic = true;
                    }
                    else
                    {
                        cprb.useGravity = true;
                        cprb.isKinematic = false;
                    }
                    EditorUtility.SetDirty(cprb);
                }
            }

            EditorUtility.SetDirty(subAsm);

            // If parent also needs a socket in the main assembly
            if (addAssemblyPart && createSocketAtPosition)
            {
                CreateSocketForPart(rootPart);
            }

            Debug.Log($"<color=cyan>[AssemblyBatchSetup]</color> Sub-Assembly '{parent.name}' created with {childCount} child parts. Base floating part: '{(firstChild != null ? firstChild.PartDisplayName : "None")}'.");
        }

        private void CreateSocketForPartUnderParent(AssemblyPart part, Transform socketParent)
        {
            string socketName = $"{part.gameObject.name}_Socket";
            GameObject socketObj = new GameObject(socketName);
            socketObj.transform.position = part.transform.position;
            socketObj.transform.rotation = part.transform.rotation;
            socketObj.transform.SetParent(socketParent, true);

            AssemblySocket socketComp = socketObj.AddComponent<AssemblySocket>();

            SerializedObject socketSo = new SerializedObject(socketComp);
            SerializedProperty targetPartProp = socketSo.FindProperty("targetPart");
            if (targetPartProp != null) targetPartProp.objectReferenceValue = part;
            SerializedProperty socketIdProp = socketSo.FindProperty("socketId");
            if (socketIdProp != null) socketIdProp.stringValue = part.PartId;
            socketSo.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(socketObj, $"Create {socketName}");

            SerializedObject partSo = new SerializedObject(part);
            SerializedProperty targetProp = partSo.FindProperty("targetSnapPoint");
            if (targetProp != null) targetProp.objectReferenceValue = socketObj.transform;
            SerializedProperty sockProp = partSo.FindProperty("targetSocket");
            if (sockProp != null) sockProp.objectReferenceValue = socketComp;
            partSo.ApplyModifiedProperties();
        }

        private void GatherChildrenRecursive(Transform parent, List<GameObject> list)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!list.Contains(child.gameObject))
                {
                    list.Add(child.gameObject);
                }
                GatherChildrenRecursive(child, list);
            }
        }

        private void SelectAllMeshRenderers()
        {
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            List<GameObject> meshObjects = new List<GameObject>();
            foreach (var mr in allRenderers)
            {
                if (mr.GetComponent<MeshFilter>() != null)
                {
                    meshObjects.Add(mr.gameObject);
                }
            }
            Selection.objects = meshObjects.ToArray();
            Debug.Log($"<color=cyan>[AssemblyBatchSetup]</color> Selected {meshObjects.Count} MeshRenderer objects.");
        }

        private void SetSameIndexDifferentGroups()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select AssemblyPart objects in the Hierarchy.", "OK");
                return;
            }

            List<AssemblyPart> parts = new List<AssemblyPart>();
            foreach (var go in selected)
            {
                AssemblyPart part = go.GetComponent<AssemblyPart>();
                if (part != null) parts.Add(part);
            }

            if (parts.Count == 0)
            {
                EditorUtility.DisplayDialog("No Parts", "None of the selected objects have an AssemblyPart component.", "OK");
                return;
            }

            // Sort by sibling index (hierarchy order)
            parts.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Set Same Index, Different Groups");

            Dictionary<string, int> groupMap = new Dictionary<string, int>();
            int nextGroup = batchStartGroupIndex;

            for (int i = 0; i < parts.Count; i++)
            {
                int grp;
                if (groupIdenticalParts)
                {
                    string key = GetPartGroupingKey(parts[i].gameObject);
                    if (!groupMap.TryGetValue(key, out grp))
                    {
                        grp = nextGroup++;
                        groupMap[key] = grp;
                    }
                }
                else
                {
                    grp = nextGroup++;
                }

                SerializedObject so = new SerializedObject(parts[i]);
                SerializedProperty orderProp = so.FindProperty("orderIndex");
                if (orderProp != null)
                {
                    orderProp.intValue = batchOrderIndex;
                }
                SerializedProperty groupProp = so.FindProperty("groupIndex");
                if (groupProp != null)
                {
                    groupProp.intValue = grp;
                }
                SerializedProperty localProp = so.FindProperty("localOrderIndex");
                if (localProp != null && parts[i].ParentSubAssembly != null)
                {
                    localProp.intValue = batchOrderIndex;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(parts[i]);
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            int totalGroups = nextGroup - batchStartGroupIndex;
            Debug.Log($"<color=cyan>[AssemblyBatchSetup]</color> Assigned shared index #{batchOrderIndex} across {parts.Count} parts into {totalGroups} distinct groups.");
        }

        [MenuItem("Tools/Engine Assembly/Quick Setup Sub-Assembly from Selection", false, 20)]
        [MenuItem("GameObject/Engine Assembly/Setup Sub-Assembly", false, 20)]
        public static void QuickSetupSubAssemblyFromSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length != 1)
            {
                EditorUtility.DisplayDialog("Sub-Assembly Setup",
                    "Please select exactly ONE parent GameObject containing child part meshes.", "OK");
                return;
            }

            GameObject parent = selected[0];
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Setup Sub-Assembly {parent.name}");

            // 1. Setup parent
            SubAssembly subAsm = parent.GetComponent<SubAssembly>();
            if (subAsm == null) subAsm = Undo.AddComponent<SubAssembly>(parent);

            AssemblyPart rootPart = parent.GetComponent<AssemblyPart>();
            if (rootPart == null) rootPart = Undo.AddComponent<AssemblyPart>(parent);
            SerializedObject rootSo = new SerializedObject(rootPart);
            SerializedProperty rOrder = rootSo.FindProperty("orderIndex");
            if (rOrder != null) rOrder.intValue = 1;
            SerializedProperty rGroup = rootSo.FindProperty("groupIndex");
            if (rGroup != null) rGroup.intValue = 1;
            rootSo.ApplyModifiedProperties();

            if (parent.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(parent);
            }

            // 2. Setup children with distinct group indices
            Dictionary<string, int> childGroupMap = new Dictionary<string, int>();
            int nextChildGroup = 1;

            int childCount = 0;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                GameObject childGo = child.gameObject;

                if (childGo.GetComponent<MeshFilter>() == null && childGo.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }

                // Add convex mesh collider
                Collider col = childGo.GetComponent<Collider>();
                if (col == null || !(col is MeshCollider mc && mc.convex))
                {
                    if (col != null) Undo.DestroyObjectImmediate(col);
                    MeshCollider newCol = Undo.AddComponent<MeshCollider>(childGo);
                    newCol.convex = true;
                }

                // Add AssemblyPart
                AssemblyPart childPart = childGo.GetComponent<AssemblyPart>();
                if (childPart == null) childPart = Undo.AddComponent<AssemblyPart>(childGo);

                int assignedChildGroup;
                string key = GetPartGroupingKey(childGo);
                if (!childGroupMap.TryGetValue(key, out assignedChildGroup))
                {
                    assignedChildGroup = nextChildGroup++;
                    childGroupMap[key] = assignedChildGroup;
                }

                SerializedObject childSo = new SerializedObject(childPart);
                SerializedProperty cOrder = childSo.FindProperty("orderIndex");
                if (cOrder != null) cOrder.intValue = 1;
                SerializedProperty cGroup = childSo.FindProperty("groupIndex");
                if (cGroup != null) cGroup.intValue = assignedChildGroup;
                SerializedProperty cLocal = childSo.FindProperty("localOrderIndex");
                if (cLocal != null) cLocal.intValue = 1;
                childSo.ApplyModifiedProperties();

                // Add Rigidbody
                if (childGo.GetComponent<Rigidbody>() == null) Undo.AddComponent<Rigidbody>(childGo);

                // Create socket if needed
                if (childPart.TargetSnapPoint == null)
                {
                    string socketName = $"{childGo.name}_Socket";
                    GameObject socketObj = new GameObject(socketName);
                    socketObj.transform.position = childGo.transform.position;
                    socketObj.transform.rotation = childGo.transform.rotation;
                    socketObj.transform.SetParent(parent.transform, true);

                    AssemblySocket socketComp = socketObj.AddComponent<AssemblySocket>();
                    SerializedObject so = new SerializedObject(socketComp);
                    SerializedProperty tp = so.FindProperty("targetPart");
                    if (tp != null) tp.objectReferenceValue = childPart;
                    SerializedProperty sid = so.FindProperty("socketId");
                    if (sid != null) sid.stringValue = childPart.PartId;
                    so.ApplyModifiedProperties();

                    Undo.RegisterCreatedObjectUndo(socketObj, $"Create {socketName}");

                    SerializedObject partSo = new SerializedObject(childPart);
                    SerializedProperty tsp = partSo.FindProperty("targetSnapPoint");
                    if (tsp != null) tsp.objectReferenceValue = socketObj.transform;
                    SerializedProperty tsock = partSo.FindProperty("targetSocket");
                    if (tsock != null) tsock.objectReferenceValue = socketComp;
                    partSo.ApplyModifiedProperties();
                }

                childCount++;
            }

            // 3. Auto-gather children and auto-number
            subAsm.AutoGatherChildren();

            // 4. Reparent child sockets to first part
            AssemblyPart firstPart = subAsm.GetFirstPart();
            subAsm.ReparentChildSocketsToFirstPart();

            // 5. Physics: first part floats, others follow gravity
            foreach (var cp in subAsm.ChildParts)
            {
                if (cp == null) continue;
                Rigidbody cprb = cp.GetComponent<Rigidbody>();
                if (cprb != null)
                {
                    Undo.RecordObject(cprb, "Configure Child Rigidbody");
                    if (cp == firstPart)
                    {
                        cprb.useGravity = false;
                        cprb.isKinematic = true;
                    }
                    else
                    {
                        cprb.useGravity = true;
                        cprb.isKinematic = false;
                    }
                    EditorUtility.SetDirty(cprb);
                }
            }

            // Create socket for root if missing
            if (rootPart.TargetSnapPoint == null)
            {
                string socketName = $"{parent.name}_Socket";
                GameObject rootSocket = new GameObject(socketName);
                rootSocket.transform.position = parent.transform.position;
                rootSocket.transform.rotation = parent.transform.rotation;
                if (parent.transform.parent != null) rootSocket.transform.SetParent(parent.transform.parent, true);

                AssemblySocket socketComp = rootSocket.AddComponent<AssemblySocket>();
                SerializedObject rso = new SerializedObject(socketComp);
                SerializedProperty rtp = rso.FindProperty("targetPart");
                if (rtp != null) rtp.objectReferenceValue = rootPart;
                SerializedProperty rsid = rso.FindProperty("socketId");
                if (rsid != null) rsid.stringValue = rootPart.PartId;
                rso.ApplyModifiedProperties();

                Undo.RegisterCreatedObjectUndo(rootSocket, $"Create {socketName}");

                SerializedObject rootPartSo = new SerializedObject(rootPart);
                SerializedProperty rtsp = rootPartSo.FindProperty("targetSnapPoint");
                if (rtsp != null) rtsp.objectReferenceValue = rootSocket.transform;
                SerializedProperty rtsock = rootPartSo.FindProperty("targetSocket");
                if (rtsock != null) rtsock.objectReferenceValue = socketComp;
                rootPartSo.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(subAsm);
            EditorUtility.SetDirty(parent);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            EditorUtility.DisplayDialog("Sub-Assembly Configured!",
                $"Sub-Assembly '{parent.name}' configured successfully with {childCount} child parts.\n\n" +
                $"• Base Floating Anchor: {(firstPart != null ? firstPart.PartDisplayName : "None")} (Floats in air)\n" +
                $"• Child Sockets: Dynamically parented to follow the base part.\n" +
                $"• Gravity: Other child parts follow gravity.", "OK");

            Debug.Log($"<color=cyan>[AssemblyBatchSetup]</color> Quick-configured Sub-Assembly '{parent.name}' with {childCount} child parts. Base anchor: '{(firstPart != null ? firstPart.PartDisplayName : "None")}'.");
        }
    }
}
