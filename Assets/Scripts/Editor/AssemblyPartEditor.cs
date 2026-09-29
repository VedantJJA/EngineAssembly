using UnityEditor;
namespace EngineAssembly.Editor
{
    [CustomEditor(typeof(AssemblyPart))]
    public class AssemblyPartEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { DrawDefaultInspector(); EditorGUILayout.HelpBox("Use Tools > Engine Assembly > Chapter Editor for chapter recipes and assembly order.",MessageType.Info); }
    }
}

