using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [CustomEditor(typeof(SubAssembly))]
    [CanEditMultipleObjects]
    public class SubAssemblyEditor : UnityEditor.Editor
    {
        private SubAssembly subAssembly;

        private void OnEnable()
        {
            subAssembly = (SubAssembly)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Status Banner
            int total = subAssembly.TotalComponentsCount;
            int snapped = subAssembly.SnappedComponentsCount;
            float progress = subAssembly.CompletionProgress;
            bool isComplete = subAssembly.IsFullyAssembled;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Sub-Assembly: {subAssembly.SubAssemblyName}", EditorStyles.boldLabel);

            if (isComplete)
            {
                GUI.color = new Color(0.4f, 1f, 0.5f);
                EditorGUILayout.LabelField("Status: FULLY ASSEMBLED (Ready to mount into parent engine)", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 0.8f, 0.3f);
                EditorGUILayout.LabelField($"Status: Incomplete ({snapped}/{total} components assembled)", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            Rect r = EditorGUILayout.GetControlRect(false, 14);
            EditorGUI.ProgressBar(r, progress, $"{snapped} / {total} Components");

            EditorGUILayout.Space(6);

            // Action Buttons Row
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Auto-Gather", "Scans hierarchy and links child parts.")))
            {
                Undo.RecordObject(subAssembly, "Auto-Gather Sub-Assembly Children");
                subAssembly.AutoGatherChildren();
                EditorUtility.SetDirty(subAssembly);
            }

            if (GUILayout.Button(new GUIContent("Auto-Number (Reverse)", "Assigns descending localOrderIndex to children based on hierarchy order.")))
            {
                Undo.RecordObject(subAssembly, "Auto-Number Children");
                subAssembly.AutoNumberChildren();
                EditorUtility.SetDirty(subAssembly);
            }

            if (GUILayout.Button(new GUIContent("Align Sockets", "Parents child sockets to first component so they dynamically move together.")))
            {
                Undo.RecordObject(subAssembly, "Align Child Sockets");
                subAssembly.ReparentChildSocketsToFirstPart();
                EditorUtility.SetDirty(subAssembly);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Child Parts Sequence Table
            if (subAssembly.ChildParts != null && subAssembly.ChildParts.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Child Parts Sequence (Local Order)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Reverse Order: Higher local index is placed FIRST. Lower is placed LAST.", MessageType.None);

                for (int i = 0; i < subAssembly.ChildParts.Count; i++)
                {
                    AssemblyPart child = subAssembly.ChildParts[i];
                    if (child == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(child.PartDisplayName, GUILayout.Width(160));

                    EditorGUI.BeginChangeCheck();
                    int newLocalIdx = EditorGUILayout.IntField(child.LocalOrderIndex, GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(child, "Change Local Order Index");
                        child.LocalOrderIndex = newLocalIdx;
                        EditorUtility.SetDirty(child);
                    }

                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(child, "Decrement Local Order");
                        child.LocalOrderIndex = Mathf.Max(1, child.LocalOrderIndex - 1);
                        EditorUtility.SetDirty(child);
                    }
                    if (GUILayout.Button("+", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(child, "Increment Local Order");
                        child.LocalOrderIndex++;
                        EditorUtility.SetDirty(child);
                    }

                    if (Application.isPlaying)
                    {
                        GUI.color = child.IsSnapped ? Color.green : Color.yellow;
                        EditorGUILayout.LabelField(child.IsSnapped ? "Snapped" : "Unsnapped", GUILayout.Width(70));
                        GUI.color = Color.white;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }

            // Draw Default Inspector properties
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Sub-Assembly Rules:\n" +
                "1. Sub-assemblies float in mid-air when detached for convenient workshop bench assembly.\n" +
                "2. When mounted in the engine, clicking on any child part removes the sub-assembly as a whole.\n" +
                "3. The sub-assembly can only be mounted into the parent engine when 100% of its internal components are assembled.\n" +
                "4. Dynamic Sockets: Child sockets follow the first part in the subassembly index-wise.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
