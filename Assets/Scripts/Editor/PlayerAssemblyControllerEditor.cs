using UnityEditor;
namespace EngineAssembly.Editor
{
    [CustomEditor(typeof(PlayerAssemblyController))]
    public class PlayerAssemblyControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { DrawDefaultInspector(); EditorGUILayout.HelpBox("Use Tools > Engine Assembly > Chapter Editor for chapter recipes and assembly order.",MessageType.Info); }
    }
}

