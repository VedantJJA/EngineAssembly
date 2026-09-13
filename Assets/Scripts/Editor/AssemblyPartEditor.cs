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

            // --- Dropdown 1: Part Properties / Configuration (Closed by default) ---
            showPropertiesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showPropertiesFoldout, "Part Properties / Configuration");
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

                // Assembly Order & Priority
                EditorGUILayout.LabelField("Assembly Order & Priority", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("orderIndex"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("prerequisiteParts"), true);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(6);

            // --- Dropdown 2: Assembly Events (Closed by default) ---
            showEventsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showEventsFoldout, "Assembly Events");
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
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Workflow Tools", EditorStyles.boldLabel);

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
