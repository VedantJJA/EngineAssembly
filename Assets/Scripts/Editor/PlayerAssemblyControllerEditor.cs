using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [CustomEditor(typeof(PlayerAssemblyController))]
    public class PlayerAssemblyControllerEditor : UnityEditor.Editor
    {
        private bool showRecordedStates = true;

        public override void OnInspectorGUI()
        {
            PlayerAssemblyController controller = (PlayerAssemblyController)target;
            serializedObject.Update();

            EditorGUILayout.Space(6);

            // --- Prominent Header: Layout & Repositioning ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Workbench Layout & Player Repositioning", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "When enabled, any engine parts you place/move while in Play Mode, as well as the player's own position/rotation, " +
                "will be permanently saved back to the scene file upon stopping Play Mode.\n\n" +
                "In the next Play Mode run, the player and parts will start from those new positions.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            SerializedProperty savePartsProp = serializedObject.FindProperty("savePlacedPartsToSceneOnExit");
            SerializedProperty savePlayerProp = serializedObject.FindProperty("savePlayerPositionOnExit");

            EditorGUILayout.PropertyField(savePartsProp, new GUIContent("Save Placed Parts on Exit"));
            EditorGUILayout.PropertyField(savePlayerProp, new GUIContent("Save Player Position on Exit"));

            EditorGUILayout.Space(6);

            if (GUILayout.Button(new GUIContent("Save Current Positions to Scene Now", "Captures and writes the current player position and placed part positions directly to the scene file right now."), GUILayout.Height(28)))
            {
                PartLayoutSaver.SaveCurrentLayoutNow();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Play Mode Runtime Disassembly Recording & Status
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Disassembly Recording & Runtime Controls", EditorStyles.boldLabel);

                Color originalBg = GUI.backgroundColor;
                if (!controller.IsRecordingDisassembly)
                {
                    GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                    if (GUILayout.Button(new GUIContent("▶ Start Disassembly Recording Mode (F6)", "Begins recording disassembly order. Disassembling parts assigns sequence indices and bypasses all previous order restrictions."), GUILayout.Height(34)))
                    {
                        controller.StartRecordingMode();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.3f);
                    if (GUILayout.Button(new GUIContent("■ Stop Disassembly Recording Mode (F6)", "Stops recording disassembly sequence and summarizes order."), GUILayout.Height(34)))
                    {
                        controller.StopRecordingMode();
                    }
                }
                GUI.backgroundColor = originalBg;

                EditorGUILayout.Space(4);

                // Index & Group Steppers
                if (controller.IsRecordingDisassembly)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Next Sequence Index: #{controller.RecordingCurrentIndex} [J / L]", EditorStyles.boldLabel);
                    if (GUILayout.Button("[-] Idx (J)", GUILayout.Width(85)))
                    {
                        controller.AdjustRecordingIndex(-1);
                    }
                    if (GUILayout.Button("[+] Idx (L)", GUILayout.Width(85)))
                    {
                        controller.AdjustRecordingIndex(1);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Group on Index: #{controller.RecordingCurrentGroup} [I / K]", EditorStyles.boldLabel);
                    if (GUILayout.Button("[-] Grp (K)", GUILayout.Width(85)))
                    {
                        controller.AdjustRecordingGroup(-1);
                    }
                    if (GUILayout.Button("[+] Grp (I)", GUILayout.Width(85)))
                    {
                        controller.AdjustRecordingGroup(1);
                    }
                    EditorGUILayout.EndHorizontal();

                    controller.AutoAdvanceIndexOnDisassemble = EditorGUILayout.Toggle(
                        new GUIContent("Auto-Advance Index", "If enabled, index auto-advances after each part. If disabled, multiple parts stay in the current group on the current index until changed with J/L or I/K."),
                        controller.AutoAdvanceIndexOnDisassemble);

                    EditorGUILayout.Space(4);
                }

                // Undo & Auto Assemble buttons
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(controller.RecordedDisassemblyStates == null || controller.RecordedDisassemblyStates.Count == 0);
                if (GUILayout.Button(new GUIContent("↩ Undo Last Step (Z)", "Reverts the last recorded disassembly step and auto-assembles that part back to its socket."), GUILayout.Height(28)))
                {
                    controller.UndoLastDisassemblyStep();
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button(new GUIContent("⚙ Auto Assemble All (Y)", "Assembles all engine parts into sockets so you can cleanly record disassembly from scratch."), GUILayout.Height(28)))
                {
                    controller.AutoAssembleAllForRecording();
                }
                EditorGUILayout.EndHorizontal();

                // Quick Status Fields
                EditorGUILayout.LabelField("Is Holding Part:", controller.IsHoldingPart ? $"YES ({controller.CurrentHeldPart.PartDisplayName})" : "No");
                EditorGUILayout.LabelField("Is Crawling:", controller.IsCrawling ? "YES (Low Stance)" : "No (Standing)");
                EditorGUILayout.LabelField("Recording Mode:", controller.IsRecordingDisassembly ? $"ACTIVE (Recorded: {controller.RecordedDisassemblyStates.Count} parts)" : "Inactive");

                // Save Recorded Disassembly to File & Scene Button
                if (controller.RecordedDisassemblyStates != null && controller.RecordedDisassemblyStates.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    Color prevC = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
                    if (GUILayout.Button(new GUIContent("💾 Save Recorded Disassembly to File & Scene", "Permanently writes the recorded disassembly sequence to Assets/RecordedDisassemblySequence.json and saves scene layout."), GUILayout.Height(28)))
                    {
                        controller.SaveRecordedDisassemblyNow();
                    }
                    GUI.backgroundColor = prevC;
                }

                // Recorded Disassembly State Info List
                if (controller.RecordedDisassemblyStates != null && controller.RecordedDisassemblyStates.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    showRecordedStates = EditorGUILayout.Foldout(showRecordedStates, $"Recorded Disassembly States ({controller.RecordedDisassemblyStates.Count} steps)", true, EditorStyles.foldoutHeader);
                    if (showRecordedStates)
                    {
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < controller.RecordedDisassemblyStates.Count; i++)
                        {
                            var state = controller.RecordedDisassemblyStates[i];
                            EditorGUILayout.BeginVertical(EditorStyles.textArea);
                            EditorGUILayout.LabelField($"Step {state.stepNumber}: {state.partDisplayName} (Index #{state.assignedIndex}, Group #{state.groupIndex})", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"  Scope: {(state.isSubAssembly ? $"Sub-Assembly '{state.subAssemblyName}'" : "Main Assembly")}");
                            EditorGUILayout.LabelField($"  Time: {state.timestamp:F1}s | Pos: {state.worldPosition}");
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6);
            }

            // Draw Default Inspector for all other settings
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
