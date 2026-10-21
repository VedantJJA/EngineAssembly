using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    public static class ExperienceMenuBuilder
    {
        public const string ScenePath="Assets/Scenes/MainMenu.unity";
        public static void ConfigureVerificationBuild()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))
                throw new System.InvalidOperationException("This entry point is for the isolated verification project.");
            Create();
            EditorBuildSettings.scenes=new[]{ScenePath,AssemblyWorkshopBuilder.ScenePath,MobileAssemblyBuilder.ScenePath,MobileEngineDisplayBuilder.ScenePath,SpatialFallbackSceneBuilder.AssemblyPath,SpatialFallbackSceneBuilder.DisplayPath,SpatialFallbackSceneBuilder.RepairPath}.Where(System.IO.File.Exists).Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        }
        [MenuItem("Tools/Engine Assembly/Create Main Menu")]
        public static void Create()
        {
            if(!System.IO.File.Exists(ScenePath))
            {
                if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
                var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
                try
                {
                    var menu=new GameObject("Experience menu").AddComponent<ExperienceMenu>();
                    var pivot=new GameObject("Engine preview").transform;
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx"),scene);
                    var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    model.transform.position-=bounds.center;model.transform.SetParent(pivot,true);pivot.position=new Vector3(1.1f,0,0);pivot.rotation=Quaternion.Euler(-15,-30,0);
                    var data=new SerializedObject(menu);data.FindProperty("enginePreview").objectReferenceValue=pivot;data.ApplyModifiedPropertiesWithoutUndo();
                    var camera=new GameObject("Menu camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-6);camera.orthographic=true;camera.orthographicSize=2.3f;camera.backgroundColor=new Color(.024f,.036f,.052f);camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();
                    var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(30,-35,0);RenderSettings.ambientLight=new Color(.12f,.17f,.2f);
                    EditorSceneManager.SaveScene(scene,ScenePath);
                }
                finally { SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
            }
            else
            {
                // Imported scenes can retain a script GUID from an isolated generation project.
                var previous=SceneManager.GetActiveScene();
                var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
                try
                {
                    var root=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Experience menu");
                    if(root && !root.GetComponent<ExperienceMenu>())
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                        var menu=root.AddComponent<ExperienceMenu>();
                        var data=new SerializedObject(menu);
                        data.FindProperty("enginePreview").objectReferenceValue=scene.GetRootGameObjects().First(o=>o.name=="Engine preview").transform;
                        data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
                    }
                }
                finally { SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
            }
            var paths=new[]{ScenePath,AssemblyWorkshopBuilder.ScenePath,MobileAssemblyBuilder.ScenePath,MobileEngineDisplayBuilder.ScenePath,SpatialFallbackSceneBuilder.AssemblyPath,SpatialFallbackSceneBuilder.DisplayPath,SpatialFallbackSceneBuilder.RepairPath}.Where(System.IO.File.Exists).ToArray();
            var settings=paths.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(EditorBuildSettings.scenes.Where(s=>!paths.Contains(s.path))).ToArray();
            EditorBuildSettings.scenes=settings;AssetDatabase.SaveAssets();Debug.Log("MAIN MENU READY: desktop and mobile prototype routes enabled.");
        }
    }
}
