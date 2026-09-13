using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [CustomEditor(typeof(AssemblyPart))]
    [CanEditMultipleObjects]
    public class AssemblyPartEditor : UnityEditor.Editor
    {
        private bool isGhostPreviewActive = false;
        private static bool showPropertiesFoldout = false;
        private static bool showEventsFoldout = false;

        public override void OnInspectorGUI()
        {
            AssemblyPart part = (AssemblyPart)target;

            // Draw Default Inspector with clean layout
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Assembly Part Mechanic:\n" +
                "- Assign 'Target Snap Point' or create a socket using the button below.\n" +
                "- In hand/selection, a translucent ghost appears at the target location.\n" +
                "- When placed within snap threshold, the part smoothly docks and locks.",
                MessageType.Info
            );
            EditorGUILayout.Space(4);

            // Status display
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Is Snapped:", part.IsSnapped ? "YES" : "No");
                EditorGUILayout.LabelField("Is Selected:", part.IsSelected ? "YES" : "No");
                EditorGUILayout.LabelField("In Snap Zone:", part.IsInSnapZone ? "YES" : "No");
                EditorGUILayout.LabelField("Order Priority:", $"{part.OrderIndex} (Prerequisites Met: {(part.ArePrerequisitesMet() ? "YES" : "NO")})");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }

            // --- Standalone Section: Assembly Order & Prerequisites ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Assembly Order & Prerequisites", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Reverse Assembly Order:\n- Higher numbers are placed FIRST during assembly.\n- Lower numbers are placed LAST (and disassembled first).", MessageType.None);

            if (part.ParentSubAssembly != null)
            {
                EditorGUILayout.HelpBox($"Belongs to Sub-Assembly: '{part.ParentSubAssembly.SubAssemblyName}'\nLocal order controls sequence within this sub-assembly.", MessageType.Info);
                SerializedProperty localOrderIndexProp = serializedObject.FindProperty("localOrderIndex");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(localOrderIndexProp, new GUIContent("Local Order Priority"));
                int nextLocal = part.CalculateNextLocalOrderIndex();
                if (GUILayout.Button(new GUIContent($"Set Next ({nextLocal})", "Recalculates next local order index within this sub-assembly."), GUILayout.Width(110)))
                {
                    localOrderIndexProp.intValue = nextLocal;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);
            }
            else if (part.SubAssemblyRoot != null)
            {
                EditorGUILayout.HelpBox($"Sub-Assembly Root: '{part.SubAssemblyRoot.SubAssemblyName}'\nOrder Priority controls when this entire sub-assembly mounts into the main engine.", MessageType.Info);
            }

            SerializedProperty orderIndexProp = serializedObject.FindProperty("orderIndex");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(orderIndexProp, new GUIContent(part.ParentSubAssembly != null ? "Global Order (Unused)" : "Order Priority"));
            int nextSuggested = part.CalculateNextOrderIndex();
            if (GUILayout.Button(new GUIContent($"Set Next ({nextSuggested})", "Recalculates the next sequential order index based on parts currently in the scene."), GUILayout.Width(110)))
            {
                orderIndexProp.intValue = nextSuggested;
            }
            EditorGUILayout.EndHorizontal();

            SerializedProperty groupIndexProp = serializedObject.FindProperty("groupIndex");
            if (groupIndexProp != null)
            {
                EditorGUILayout.PropertyField(groupIndexProp, new GUIContent("Group on Index", "Parts sharing the same orderIndex and groupIndex share equal priority (e.g. 4 spark plugs, 8 bolts)."));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteParts"), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // --- Dropdown 1: Part Properties / Configuration (Closed by default) ---
            showPropertiesFoldout = EditorGUILayout.Foldout(showPropertiesFoldout, "Part Properties / Configuration", true, EditorStyles.foldoutHeader);
            if (showPropertiesFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Part Information
                EditorGUILayout.LabelField("Part Information", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("partId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("partDisplayName"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("geometryGroupId"));
                EditorGUILayout.Space(6);

                // Snapping Destination
                EditorGUILayout.LabelField("Snapping Destination", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSnapPoint"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSocket"));
                EditorGUILayout.Space(6);

                // Snap Thresholds
                EditorGUILayout.LabelField("Snap Thresholds", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapDistanceThreshold"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("requireRotationMatch"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapAngleThreshold"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapOnRelease"));
                EditorGUILayout.Space(6);

                // Snap Animation
                EditorGUILayout.LabelField("Snap Animation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothSnap"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parentToTargetOnSnap"));
                EditorGUILayout.Space(6);

                // Ghost Preview Settings
                EditorGUILayout.LabelField("Ghost Preview Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showGhostOnSelect"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ghostRevealDistance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ghostMaterial"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ghostDefaultColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ghostInRangeColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ghostLockedColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("customGhostPrefab"));
                EditorGUILayout.Space(6);

                // Snap Confirmation Flash
                EditorGUILayout.LabelField("Snap Confirmation Flash", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flashGreenOnSnap"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapFlashCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapFlashDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("snapFlashColor"));
                EditorGUILayout.Space(6);

                // Sub-Assembly Hierarchy (Optional)
                EditorGUILayout.LabelField("Sub-Assembly Hierarchy (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("subAssemblyRoot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parentSubAssembly"));
                EditorGUILayout.Space(6);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);

            // --- Dropdown 2: Assembly Events (Closed by default) ---
            showEventsFoldout = EditorGUILayout.Foldout(showEventsFoldout, "Assembly Events", true, EditorStyles.foldoutHeader);
            if (showEventsFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("Hook up custom callbacks, audio, particle effects, or scripts when interacting with this part.", MessageType.None);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onSelected"), new GUIContent("After Picking Up (On Selected)"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onSnapped"), new GUIContent("After Placing / Snapping (On Snapped)"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onUnsnapped"), new GUIContent("After Removing / Disassembling (On Unsnapped)"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onDeselected"), new GUIContent("After Dropping / Deselecting (On Deselected)"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onSnapRejected"), new GUIContent("On Snap Rejected (Locked Action)"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onEnterSnapZone"), new GUIContent("On Enter Snap Zone"), true);
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("onExitSnapZone"), new GUIContent("On Exit Snap Zone"), true);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Workflow Tools", EditorStyles.boldLabel);

            SubAssembly parentSub = part.ParentSubAssembly;
            if (parentSub == null && part.transform.parent != null)
            {
                parentSub = part.transform.parent.GetComponent<SubAssembly>();
                if (parentSub == null)
                {
                    parentSub = part.transform.parent.GetComponentInParent<SubAssembly>();
                }
            }

            // Sub-Assembly Socket Generator (Appears if parent has SubAssembly)
            if (parentSub != null)
            {
                AssemblyPart firstPart = parentSub.GetFirstPart();
                bool isFirstPart = (firstPart == part);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Sub-Assembly Socket Workflow", EditorStyles.boldLabel);

                string roleText = isFirstPart
                    ? "Role: Base / First Part (Floats in air - workshop anchor for sub-assembly)"
                    : $"Role: Child Part (Follows gravity - socket tracks '{(firstPart != null ? firstPart.PartDisplayName : "Base Part")}')";

                EditorGUILayout.HelpBox(
                    $"Parent Sub-Assembly: '{parentSub.SubAssemblyName}'\n{roleText}",
                    isFirstPart ? MessageType.Info : MessageType.None
                );

                GUI.backgroundColor = isFirstPart ? new Color(0.35f, 0.9f, 0.65f) : new Color(0.35f, 0.82f, 1f);
                string btnLabel = isFirstPart
                    ? "[Generate Sub-Assembly Socket (Base Floating Anchor)]"
                    : "[Generate Sub-Assembly Socket (Follow Base Part)]";

                if (GUILayout.Button(new GUIContent(btnLabel, "Generates/updates socket for this sub-assembly part. First part floats in air; other parts follow gravity and their sockets follow the first part."), GUILayout.Height(32)))
                {
                    GenerateSubAssemblySocket(part, parentSub);
                }
                GUI.backgroundColor = Color.white;

                if (!isFirstPart)
                {
                    if (GUILayout.Button(new GUIContent("Make This The Base / Floating Part", "Sets this part's local order priority higher than other parts so it becomes the base part of the sub-assembly."), EditorStyles.miniButton))
                    {
                        SetAsBaseSubAssemblyPart(part, parentSub);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }

            // 1. Create Socket Button
            if (GUILayout.Button("[Create Snap Socket at Current Position]", GUILayout.Height(30)))
            {
                CreateSocketAtCurrentPosition(part);
            }

            // 2. Ghost Preview Toggle Button
            string ghostBtnText = isGhostPreviewActive ? "[Hide Ghost Preview]" : "[Preview Ghost at Target]";
            if (GUILayout.Button(ghostBtnText, GUILayout.Height(28)))
            {
                isGhostPreviewActive = !isGhostPreviewActive;
                part.PreviewGhostInEditor(isGhostPreviewActive);
                SceneView.RepaintAll();
            }

            // 3. Test Snap Alignment
            if (GUILayout.Button("[Snap to Target]", GUILayout.Height(26)))
            {
                if (part.TargetSnapPoint != null)
                {
                    Undo.RecordObject(part.transform, "Snap Part to Target");
                    part.transform.position = part.TargetSnapPoint.position;
                    part.transform.rotation = part.TargetSnapPoint.rotation;
                }
                else
                {
                    EditorUtility.DisplayDialog("Missing Target", "Please assign a Target Snap Point or create a socket first!", "OK");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void GenerateSubAssemblySocket(AssemblyPart part, SubAssembly parentSub)
        {
            if (part == null || parentSub == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Sub-Assembly Socket");

            // 1. Ensure sub-assembly hierarchy and child references are linked
            parentSub.AutoGatherChildren();
            part.ParentSubAssembly = parentSub;
            EditorUtility.SetDirty(parentSub);

            // 2. Identify the first part (base part) of this sub-assembly
            AssemblyPart firstPart = parentSub.GetFirstPart();
            bool isFirstPart = (firstPart == part);

            // 3. Determine socket anchor:
            // - The first part anchors under the SubAssembly root GameObject.
            // - All other parts anchor to firstPart.transform so they dynamically follow it.
            Transform anchor;
            if (isFirstPart)
            {
                anchor = (parentSub.gameObject == part.gameObject) ? parentSub.transform.parent : parentSub.transform;
            }
            else
            {
                anchor = (firstPart != null) ? firstPart.transform : parentSub.transform;
            }

            // 4. Create or reuse socket GameObject
            Transform existingTarget = part.TargetSnapPoint;
            GameObject socketObj = null;
            AssemblySocket socketComp = null;

            if (existingTarget != null)
            {
                socketObj = existingTarget.gameObject;
                socketComp = existingTarget.GetComponent<AssemblySocket>();
            }

            if (socketObj == null)
            {
                string socketName = $"{part.gameObject.name}_Socket";
                socketObj = new GameObject(socketName);
                Undo.RegisterCreatedObjectUndo(socketObj, $"Create {socketName}");
            }
            else
            {
                Undo.RecordObject(socketObj.transform, "Reparent Sub-Assembly Socket");
            }

            // Position socket at part's current assembled pose
            socketObj.transform.position = part.transform.position;
            socketObj.transform.rotation = part.transform.rotation;

            if (anchor != null)
            {
                socketObj.transform.SetParent(anchor, true);
            }

            // 5. Setup AssemblySocket component
            if (socketComp == null)
            {
                socketComp = socketObj.GetComponent<AssemblySocket>();
                if (socketComp == null)
                {
                    socketComp = Undo.AddComponent<AssemblySocket>(socketObj);
                }
            }

            SerializedObject socketSo = new SerializedObject(socketComp);
            SerializedProperty targetPartProp = socketSo.FindProperty("targetPart");
            if (targetPartProp != null) targetPartProp.objectReferenceValue = part;
            SerializedProperty socketIdProp = socketSo.FindProperty("socketId");
            if (socketIdProp != null) socketIdProp.stringValue = part.PartId;
            socketSo.ApplyModifiedProperties();

            // 6. Link to part serialized fields
            Undo.RecordObject(part, "Link Sub-Assembly Socket");
            SerializedProperty targetProp = serializedObject.FindProperty("targetSnapPoint");
            if (targetProp != null) targetProp.objectReferenceValue = socketObj.transform;
            SerializedProperty socketProp = serializedObject.FindProperty("targetSocket");
            if (socketProp != null) socketProp.objectReferenceValue = socketComp;
            SerializedProperty parentProp = serializedObject.FindProperty("parentSubAssembly");
            if (parentProp != null) parentProp.objectReferenceValue = parentSub;
            serializedObject.ApplyModifiedProperties();

            // 7. Configure Rigidbody: first part floats, other parts follow gravity
            Rigidbody rb = part.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(part.gameObject);
            }

            if (rb != null)
            {
                Undo.RecordObject(rb, "Configure Rigidbody Gravity");
                if (isFirstPart)
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }
                else
                {
                    rb.useGravity = true;
                    rb.isKinematic = false;
                }
                EditorUtility.SetDirty(rb);
            }

            // 8. Re-parent any other existing child sockets under firstPart to ensure consistency
            parentSub.ReparentChildSocketsToFirstPart();

            EditorUtility.SetDirty(part);
            EditorUtility.SetDirty(socketObj);

            Selection.activeGameObject = socketObj;

            Debug.Log($"<color=cyan>[AssemblyPartEditor]</color> Configured Sub-Assembly Socket for '<b>{part.PartDisplayName}</b>'. Anchor: '{(anchor != null ? anchor.name : "None")}', CanFloat: {isFirstPart}, UseGravity: {!isFirstPart}");
        }

        private void SetAsBaseSubAssemblyPart(AssemblyPart part, SubAssembly parentSub)
        {
            Undo.RecordObject(part, "Set As Base Sub-Assembly Part");
            int maxLocal = 0;
            if (parentSub.ChildParts != null)
            {
                foreach (var p in parentSub.ChildParts)
                {
                    if (p != null && p.LocalOrderIndex > maxLocal) maxLocal = p.LocalOrderIndex;
                }
            }

            SerializedProperty localProp = serializedObject.FindProperty("localOrderIndex");
            if (localProp != null)
            {
                localProp.intValue = maxLocal + 1;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                part.LocalOrderIndex = maxLocal + 1;
            }

            GenerateSubAssemblySocket(part, parentSub);
        }

        private void CreateSocketAtCurrentPosition(AssemblyPart part)
        {
            Undo.IncrementCurrentGroup();
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
            Undo.RecordObject(part, "Assign Snap Target");

            SerializedProperty targetProp = serializedObject.FindProperty("targetSnapPoint");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = socketObj.transform;
            }

            SerializedProperty socketProp = serializedObject.FindProperty("targetSocket");
            if (socketProp != null)
            {
                socketProp.objectReferenceValue = socketComp;
            }

            serializedObject.ApplyModifiedProperties();
            Selection.activeGameObject = socketObj;

            Debug.Log($"<color=cyan>[AssemblyPartEditor]</color> Created snap socket '{socketName}' at {socketObj.transform.position}");
        }

        private void OnSceneGUI()
        {
            AssemblyPart part = (AssemblyPart)target;
            if (part == null || part.TargetSnapPoint == null) return;

            Transform targetTransform = part.TargetSnapPoint;

            // Draw line connecting part to snap target
            Handles.color = part.IsSnapped ? Color.green : new Color(0.2f, 0.8f, 1f, 0.7f);
            Handles.DrawDottedLine(part.transform.position, targetTransform.position, 4f);

            // Distance label
            float distance = Vector3.Distance(part.transform.position, targetTransform.position);
            Vector3 midPoint = (part.transform.position + targetTransform.position) * 0.5f;
            Handles.Label(midPoint, $"{part.PartDisplayName}: {distance:F2}m");

            // Wire disc showing snap target
            Handles.color = new Color(0.2f, 1f, 0.4f, 0.4f);
            Handles.DrawWireDisc(targetTransform.position, targetTransform.up, 0.15f);
        }
    }
}
