using UnityEditor;
using UnityEngine;

namespace EngineAssembly
{
    [CustomEditor(typeof(AutoAssemblyController))]
    public class AutoAssemblyControllerEditor : Editor
    {
        private AutoAssemblyController controller;

        private void OnEnable()
        {
            controller = (AutoAssemblyController)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Status Banner
            AssemblyManager manager = AssemblyManager.Instance;
            int totalParts = manager != null ? manager.TotalPartsCount : 0;
            int snappedParts = manager != null ? manager.SnappedPartsCount : 0;
            float progress = totalParts > 0 ? (float)snappedParts / totalParts : 0f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Auto Assembly System", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Assembly Progress: {snappedParts} / {totalParts} ({progress:P0})");
            Rect r = EditorGUILayout.GetControlRect(false, 14);
            EditorGUI.ProgressBar(r, progress, $"{snappedParts} / {totalParts} Parts");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Action Buttons
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions (Play Mode)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("Assemble Whole Engine", GUILayout.Height(32)))
            {
                controller.AssembleWhole();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Assemble >= Index ({controller.TargetOrderIndex})", GUILayout.Height(26)))
            {
                controller.AssembleToOrderIndex(controller.TargetOrderIndex);
            }
            if (GUILayout.Button($"Assemble {controller.TargetPartCount} Parts", GUILayout.Height(26)))
            {
                controller.AssembleToPartCount(controller.TargetPartCount);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Assemble Next Step", GUILayout.Height(26)))
            {
                controller.AssembleNextStep();
            }

            GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);
            if (GUILayout.Button("Disassemble All", GUILayout.Height(26)))
            {
                controller.DisassembleAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (controller.IsAssembling)
            {
                EditorGUILayout.Space(4);
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Stop Running Assembly"))
                {
                    controller.StopAutoAssembly();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to execute animated and interactive assembly actions.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Default Property Fields
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Reverse Order Logic: Parts with higher Order Priority numbers are assembled first, while parts with lower numbers are assembled last.", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
