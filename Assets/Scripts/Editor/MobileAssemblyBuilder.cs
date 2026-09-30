using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace EngineAssembly.Editor
{
    public static class MobileAssemblyBuilder
    {
        public const string ScenePath="Assets/Scenes/MobileAssembly.unity";
        [MenuItem("Tools/Engine Assembly/Create Mobile Assembly Scene")]
        public static void Create()
        {
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity",OpenSceneMode.Single);
            if(File.Exists(ScenePath)) { EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));return; }
            var recipe=AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx");
            if(!model)throw new InvalidOperationException("Missing labeled engine model.");
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var manager=new GameObject("Mobile Assembly").AddComponent<AssemblyManager>();
                var root=new GameObject("Floating Engine Anchor").transform;
                var engine=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
                PrefabUtility.UnpackPrefabInstance(engine,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);engine.transform.SetParent(root,true);
                var parts=new List<AssemblyPart>();
                foreach(var filter in engine.GetComponentsInChildren<MeshFilter>(true))
                {
                    var definition=recipe.parts.FirstOrDefault(d=>d.id==filter.name);if(definition==null)continue;
                    var part=AssemblyBatchSetup.SetupPart(filter.gameObject);part.SetIdentity(definition.id,definition.displayName);
                    if(!part.GetComponent<Collider>()) { var collider=part.gameObject.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=filter.sharedMesh; }
                    var group=recipe.subAssemblies?.FirstOrDefault(g=>g.id==definition.subAssemblyId);
                    var carrier=group==null?null:recipe.parts.First(d=>d.id==group.rootPartId);
                    var position=carrier==null?definition.targetPosition:carrier.targetPosition+carrier.targetRotation*definition.targetPosition;
                    var rotation=carrier==null?definition.targetRotation:carrier.targetRotation*definition.targetRotation;
                    part.transform.SetPositionAndRotation(position,rotation);
                    part.TargetSocket.transform.SetParent(root,true);part.TargetSocket.transform.SetPositionAndRotation(position,rotation);
                    parts.Add(part);
                }
                if(parts.Count!=recipe.parts.Count-(recipe.subAssemblies?.Count??0))throw new InvalidOperationException("Recipe/model coverage mismatch.");
                var storage=new GameObject("Offscreen Part Storage").transform;storage.position=new Vector3(1000,0,0);
                manager.Configure(root,parts,AssetDatabase.LoadAssetAtPath<TextAsset>(AssemblyWorkshopBuilder.RecipePath));
                var serialized=new SerializedObject(manager);serialized.FindProperty("showWorkshopUI").boolValue=false;serialized.FindProperty("trayFrame").objectReferenceValue=storage;serialized.ApplyModifiedPropertiesWithoutUndo();
                var camera=new GameObject("Mobile Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=42;camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.backgroundColor=new Color(.035f,.052f,.075f);camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(0,0,-7);
                camera.gameObject.AddComponent<AudioListener>();var cameraData=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                var light=new GameObject("Studio Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-35,0);
                RenderSettings.ambientLight=new Color(.55f,.6f,.68f);
                var renderers=parts.SelectMany(p=>p.GetComponentsInChildren<Renderer>()).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                var art=GenerateArtwork(parts,camera);
                manager.gameObject.AddComponent<MobileAssemblyController>().Configure(camera,bounds.extents.magnitude,art,bounds.center);
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                Debug.Log("Created mobile assembly scene with "+parts.Count+" part cards.");
            }
            finally { if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
        }
        static MobilePartArtwork GenerateArtwork(List<AssemblyPart> parts,Camera camera)
        {
            const string path="Assets/UI/MobilePartArtwork.asset";
            var art=AssetDatabase.LoadAssetAtPath<MobilePartArtwork>(path);
            if(!art) { art=ScriptableObject.CreateInstance<MobilePartArtwork>();AssetDatabase.CreateAsset(art,path); }
            Directory.CreateDirectory("Assets/UI/MobileParts");
            var originalMask=camera.cullingMask;var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;var originalFov=camera.fieldOfView;
            camera.cullingMask=1<<29;camera.fieldOfView=32;
            foreach(var part in parts)
            {
                if(art.Find(part.PartId))continue;
                var renderers=part.GetComponentsInChildren<MeshRenderer>().Where(r=>part.Manager?part.Manager.OwnsMesh(part,r.GetComponent<MeshFilter>()):r.GetComponentInParent<AssemblyPart>()==part).ToArray();
                if(renderers.Length==0)continue;
                var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
                var bounds=renderers[0].bounds;foreach(var r in renderers) { bounds.Encapsulate(r.bounds);r.gameObject.layer=29; }
                float distance=Mathf.Max(.015f,bounds.extents.magnitude)*4.5f;
                camera.nearClipPlane=Mathf.Max(.001f,distance*.01f);camera.transform.position=bounds.center+new Vector3(1,.65f,-1).normalized*distance;camera.transform.LookAt(bounds.center);
                string filename="Assets/UI/MobileParts/"+part.PartId+".png";
                WorkshopPresentationBuilder.Render(camera,filename,256,180);
                for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];
                AssetDatabase.ImportAsset(filename);
                var importer=(TextureImporter)AssetImporter.GetAtPath(filename);importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.SaveAndReimport();
                art.parts.Add(new MobilePartArtwork.Entry{id=part.PartId,image=AssetDatabase.LoadAssetAtPath<Texture2D>(filename)});
            }
            camera.cullingMask=originalMask;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);camera.fieldOfView=originalFov;camera.nearClipPlane=.05f;
            EditorUtility.SetDirty(art);return art;
        }
        public static void GenerateSubAssemblyArtworkBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run in the isolated verification project.");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();
            manager.LoadRecipe(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath));
            manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(manager.Recipe.chapters.Count-1);
            var carriers=manager.AssemblyParts.Where(p=>manager.CarrierDefinition(p.PartId)!=null).ToList();
            foreach(var part in carriers)
            {
                var group=manager.CarrierDefinition(part.PartId);var box=part.GetComponent<BoxCollider>();
                group.focusCenter=box.center;group.focusRadius=box.size.magnitude*.55f;
            }
            GenerateArtwork(carriers,Camera.main);AssetDatabase.SaveAssets();
            AssemblyRecipeStore.Save(manager.Recipe,"three-cylinder");
            Debug.Log("Generated "+carriers.Count+" complete-unit thumbnails and measured fixture bounds.");
        }
        public static void GenerateWiringArtworkBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run in the isolated verification project.");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();
            manager.LoadRecipe(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath));
            manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(manager.Recipe.chapters.Count-1);
            var art=AssetDatabase.LoadAssetAtPath<MobilePartArtwork>("Assets/UI/MobilePartArtwork.asset");
            art.parts.RemoveAll(p=>p.id=="909_Motor_Wiring_01");
            GenerateArtwork(new List<AssemblyPart>{manager.FindPart("909_Motor_Wiring_01")},Camera.main);AssetDatabase.SaveAssets();
        }
    }
}

