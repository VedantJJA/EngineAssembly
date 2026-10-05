using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace EngineAssembly.Editor
{
    public static class MobileEngineDisplayBuilder
    {
        public const string ScenePath="Assets/Scenes/MobileEngineDisplay.unity";
        [MenuItem("Tools/Engine Assembly/Create Mobile Engine Display Scene")]
        public static void Create()
        {
            if(System.IO.File.Exists(ScenePath)){EnsureBuildScene();EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));return;}
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(MobileAssemblyBuilder.ScenePath,OpenSceneMode.Single);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab");if(!prefab)throw new InvalidOperationException("Missing collider engine prefab.");
                var source=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var recipeAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(AssemblyWorkshopBuilder.RecipePath);var recipe=AssemblyRecipeStore.Parse(recipeAsset.text);
                var root=new GameObject("Display Engine").transform;
                int count=0;
                // Copy visuals only: no gameplay rigidbodies, convex hulls, sockets or player components.
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!recipe.parts.Any(d=>d.id==filter.name) || !filter.GetComponent<MeshRenderer>() || !filter.sharedMesh)continue;
                    var node=new GameObject(filter.name);node.transform.SetParent(root,false);node.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);node.transform.localScale=filter.transform.lossyScale;
                    node.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;node.AddComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;count++;
                }
                UnityEngine.Object.DestroyImmediate(source);
                if(count!=recipe.parts.Count-recipe.subAssemblies.Count)throw new InvalidOperationException("Display geometry coverage mismatch: "+count);
                root.rotation=Quaternion.Euler(-12,-25,0);
                var camera=new GameObject("Display Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=42;camera.nearClipPlane=.025f;camera.farClipPlane=80;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.052f,.075f);camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                var key=new GameObject("Display Key Light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(35,-35,0);
                var fill=new GameObject("Display Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(15,140,0);RenderSettings.ambientLight=new Color(.5f,.57f,.65f);
                new GameObject("Mobile Engine Display").AddComponent<MobileEngineDisplay>().Configure(root,camera,recipeAsset);
                EditorSceneManager.SaveScene(scene,ScenePath);EnsureBuildScene();AssetDatabase.SaveAssets();Debug.Log("DISPLAY SCENE READY: "+count+" physical components.");
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        public static void CreateBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Run this entry point in the isolated verification project.");
            Create();ExperienceMenuBuilder.ConfigureVerificationBuild();
        }
        static void EnsureBuildScene()
        {
            if(EditorBuildSettings.scenes.Any(s=>s.path==ScenePath && s.enabled))return;
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        }
    }
}
