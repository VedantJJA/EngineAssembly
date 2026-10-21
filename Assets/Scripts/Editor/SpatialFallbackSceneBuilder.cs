using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    public static class SpatialFallbackSceneBuilder
    {
        public const string AssemblyPath="Assets/Scenes/MobileAssemblyARFallback.unity";
        public const string DisplayPath="Assets/Scenes/MobileDisplayARFallback.unity";
        public const string RepairPath="Assets/Scenes/RepairVRFallback.unity";
        [MenuItem("Tools/Engine Assembly/Create AR and VR Fallback Scenes")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before creating fallback scenes.");
            CreateCopy(MobileAssemblyBuilder.ScenePath,AssemblyPath,false);
            CreateCopy(MobileEngineDisplayBuilder.ScenePath,DisplayPath,false);
            CreateCopy(AssemblyWorkshopBuilder.ScenePath,RepairPath,true);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();Debug.Log("Spatial fallback scenes ready. Original scenes preserved.");
        }
        static void CreateCopy(string source,string destination,bool repair)
        {
            if(File.Exists(destination))return;
            if(!AssetDatabase.CopyAsset(source,destination))throw new InvalidOperationException("Unable to copy scene: "+source);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(destination,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots=scene.GetRootGameObjects();
                var manager=roots.SelectMany(r=>r.GetComponentsInChildren<AssemblyManager>()).FirstOrDefault();
                var display=roots.SelectMany(r=>r.GetComponentsInChildren<MobileEngineDisplay>()).FirstOrDefault();
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
                var host=manager?manager.gameObject:display.gameObject;
                var root=manager?manager.AssemblyRoot:new SerializedObject(display).FindProperty("engineRoot").objectReferenceValue as Transform;
                SpatialExperienceRig.Ensure(host,root,camera);
                if(repair)
                {
                    host.AddComponent<RepairVRFallback>();
                    if(!host.GetComponent<PCRepairChapter>())host.AddComponent<PCRepairChapter>();
                    var player=roots.FirstOrDefault(r=>r.GetComponent<PlayerAssemblyController>());
                    if(player)
                    {
                        var rig=new GameObject("Fallback locomotion rig · replace with XR Origin").transform;
                        player.transform.SetParent(rig,true);
                    }
                }
                else camera.backgroundColor=new Color(.94f,.97f,.98f);
                EditorSceneManager.SaveScene(scene,destination);
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        public static void CreateBatch(){Create();EditorApplication.Exit(0);}
        public static void RegisterBuildScenes()
        {
            var paths=new[]{AssemblyPath,DisplayPath,RepairPath};
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>!paths.Contains(s.path)).Concat(paths.Where(File.Exists).Select(p=>new EditorBuildSettingsScene(p,true))).ToArray();
            // ProjectSettings needs the project save flush; SaveAssetIfDirty
            // alone does not write the global scene list in Unity 6.
            AssetDatabase.SaveAssets();
        }
    }
}
