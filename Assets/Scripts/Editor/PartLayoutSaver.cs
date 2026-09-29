using UnityEditor;
namespace EngineAssembly.Editor
{
    public static class PartLayoutSaver
    {
        [MenuItem("Tools/Engine Assembly/Chapter Editor")]
        public static void Open() => AssemblyChapterEditor.Open();
    }
}
