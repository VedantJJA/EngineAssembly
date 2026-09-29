using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public class AssemblyRecipeCatalogBuilder : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p=>p.StartsWith("Assets/AssemblyRecipes/") && p.EndsWith(".json")))EditorApplication.delayCall+=Rebuild;
        }
        public static void Rebuild()
        {
            const string path="Assets/Resources/AssemblyRecipeCatalog.asset";
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            var catalog=AssetDatabase.LoadAssetAtPath<AssemblyRecipeCatalog>(path);
            if(!catalog) { catalog=ScriptableObject.CreateInstance<AssemblyRecipeCatalog>();AssetDatabase.CreateAsset(catalog,path); }
            catalog.recipes=AssetDatabase.FindAssets("t:TextAsset",new[]{"Assets/AssemblyRecipes"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".json")).OrderBy(p=>p).Select(AssetDatabase.LoadAssetAtPath<TextAsset>).ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
    }
}
