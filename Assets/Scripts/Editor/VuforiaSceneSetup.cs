using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class VuforiaSceneSetup
    {
        public const string AssemblyPath="Assets/Scenes/MobileAssemblyAR.unity";
        public const string DisplayPath="Assets/Scenes/MobileDisplayAR.unity";
        static VuforiaSceneSetup(){EditorApplication.delayCall+=ConfigureSDK;if(SessionState.GetBool("VuforiaSetup.Pending",false))EditorApplication.delayCall+=CreateBatch;}
        [MenuItem("Tools/Engine Assembly/AR/Configure Vuforia SDK")]
        public static void ConfigureSDK()
        {
            bool present=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.name=="com.ptc.vuforia.engine");
            foreach(var target in new[]{NamedBuildTarget.Standalone,NamedBuildTarget.Android,NamedBuildTarget.iOS})
            {
                var defines=PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s=>s!="ENGINE_VUFORIA" && !string.IsNullOrEmpty(s)).ToList();
                if(present)defines.Add("ENGINE_VUFORIA");
                string updated=string.Join(";",defines);
                if(updated!=PlayerSettings.GetScriptingDefineSymbols(target))PlayerSettings.SetScriptingDefineSymbols(target,updated);
            }
#if ENGINE_VUFORIA
            if(present)
            {
                var configuration=Vuforia.VuforiaConfiguration.Instance;
                if(!AssetDatabase.Contains(configuration))AssetDatabase.CreateAsset(configuration,"Assets/Resources/VuforiaConfiguration.asset");
                configuration.Vuforia.DelayedInitialization=true;
                configuration.DeviceTracker.AutoInitAndStartTracker=true;
                configuration.DeviceTracker.AutoImportArcoreSetting=true;
                configuration.DeviceTracker.ARCoreRequirementSetting=Vuforia.VuforiaConfiguration.DeviceTrackerConfiguration.ARCoreRequirement.OPTIONAL;
                EditorUtility.SetDirty(configuration);AssetDatabase.SaveAssets();
            }
#endif
            if(string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription))PlayerSettings.iOS.cameraUsageDescription="Use the camera to place and explore the engine in augmented reality.";
            if(present)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                if((int)PlayerSettings.Android.minSdkVersion<29)PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
                var identifier=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                if(string.IsNullOrEmpty(identifier) || identifier.Contains("com.unity.template."))PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.engineassembly.app");
                // Vulkan support is experimental in Vuforia's Unity extension.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
                AssetDatabase.SaveAssets();
            }
        }
        [MenuItem("Tools/Engine Assembly/AR/Create Vuforia Mobile Scenes")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before creating AR scenes.");
            if(!IsCurrent(AssemblyPath))BuildFresh(SpatialFallbackSceneBuilder.AssemblyPath,AssemblyPath,"MobileAssemblyARFallback");
            if(!IsCurrent(DisplayPath))BuildFresh(SpatialFallbackSceneBuilder.DisplayPath,DisplayPath,"MobileDisplayARFallback");
            Register();
        }
        [MenuItem("Tools/Engine Assembly/AR/Rebuild Vuforia AR Scenes From Scratch")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before rebuilding AR scenes.");
            BuildFresh(SpatialFallbackSceneBuilder.AssemblyPath,AssemblyPath,"MobileAssemblyARFallback");
            BuildFresh(SpatialFallbackSceneBuilder.DisplayPath,DisplayPath,"MobileDisplayARFallback");
            Register();
        }
        static void Register()
        {
            var paths=new[]{AssemblyPath,DisplayPath};
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>!paths.Contains(s.path)).Concat(paths.Select(p=>new EditorBuildSettingsScene(p,true))).ToArray();
            ConfigureSDK();AssetDatabase.SaveAssets();
        }
        static bool IsCurrent(string path)
        {
            if(!File.Exists(path))return false;
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
#if ENGINE_VUFORIA
                var providers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VuforiaGroundPlaneProvider>(true)).ToArray();
                return providers.Length==1 && providers[0].SceneConfigured;
#else
                return false;
#endif
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        static void BuildFresh(string source,string destination,string fallback)
        {
            // Reuse only the authored engine/content data from the non-AR
            // experience. Nothing is copied from the previous AR scene.
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ExperienceMenuBuilder.ScenePath,OpenSceneMode.Single);
            var previous=SceneManager.GetActiveScene();string previousPath=previous.path;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var existing=SceneManager.GetSceneByPath(destination);
            bool replaceOpen=existing.IsValid() && existing.isLoaded;
            if(replaceOpen)EditorSceneManager.CloseScene(existing,true);
            Scene preview=default;
            try
            {
                preview=EditorSceneManager.OpenPreviewScene(source);
                var bundle=new GameObject("Engine Experience");SceneManager.MoveGameObjectToScene(bundle,preview);
                foreach(var root in preview.GetRootGameObjects().Where(r=>r!=bundle))root.transform.SetParent(bundle.transform,true);
                var content=UnityEngine.Object.Instantiate(bundle);content.name="Engine Experience";SceneManager.MoveGameObjectToScene(content,scene);
                EditorSceneManager.ClosePreviewScene(preview);preview=default;
                var host=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpatialExperienceRig>()).Single().gameObject;
                var provider=host.AddComponent<VuforiaGroundPlaneProvider>();
                host.AddComponent<MobileARSession>().Configure(provider,fallback);
#if ENGINE_VUFORIA
                var factory=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Vuforia.Unity.Editor").GetType("Vuforia.EditorClasses.GameObjectFactory");
                Func<string,GameObject> create=method=>(GameObject)factory.GetMethod(method).Invoke(null,null);
                var ground=new GameObject("Vuforia Ground Plane").transform;
                var camera=create("CreateARCamera");camera.transform.SetParent(ground,false);
                camera.GetComponent<Vuforia.VuforiaBehaviour>().enabled=false;
                camera.GetComponent<Camera>().enabled=false;camera.GetComponent<Camera>().nearClipPlane=.01f;camera.GetComponent<Camera>().farClipPlane=50;
                camera.GetComponent<AudioListener>().enabled=false;
                foreach(var component in camera.GetComponents<MonoBehaviour>().Where(c=>c.GetType().Name=="DefaultInitializationErrorHandler"))UnityEngine.Object.DestroyImmediate(component);
                var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();if(!cameraData)cameraData=camera.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing=false;cameraData.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.None;
                var stage=create("CreateGroundPlaneStage");stage.transform.SetParent(ground,false);
                // Session controls visibility; an SDK observer handler must not
                // toggle gameplay children while the camera initializes.
                foreach(var handler in stage.GetComponents<DefaultObserverEventHandler>())UnityEngine.Object.DestroyImmediate(handler);
                var plane=create("CreatePlaneFinder");plane.transform.SetParent(ground,false);
                UnityEngine.Object.DestroyImmediate(plane.GetComponent<Vuforia.AnchorInputListenerBehaviour>());
                var indicator=new GameObject("Detected Surface Pose");indicator.transform.SetParent(plane.transform,false);
                var finder=plane.GetComponent<Vuforia.PlaneFinderBehaviour>();finder.PlaneIndicator=indicator;finder.Height=1.2f;finder.HitTestMode=Vuforia.HitTestMode.AUTOMATIC;
                finder.OnAutomaticHitTest=new Vuforia.HitTestEvent();finder.OnInteractiveHitTest=new Vuforia.HitTestEvent();
                var positioning=plane.GetComponent<Vuforia.ContentPositioningBehaviour>();positioning.AnchorStage=stage.GetComponent<Vuforia.AnchorBehaviour>();positioning.DuplicateStage=false;positioning.OnContentPlaced=new Vuforia.ContentPlacedEvent();
                provider.Configure(ground,camera.GetComponent<Camera>(),finder,positioning,positioning.AnchorStage,indicator);
                Persist(finder.OnAutomaticHitTest,provider.HandleSurfaceHit);Persist(finder.OnInteractiveHitTest,provider.HandleSurfaceHit);Persist(positioning.OnContentPlaced,provider.HandleContentPlaced);
#endif
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
                if(!EditorSceneManager.SaveScene(scene,destination))throw new IOException("Unable to save rebuilt AR scene: "+destination);
            }
            finally
            {
                if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
                if(previousPath==destination)SceneManager.SetActiveScene(scene);
                else{if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
            }
        }
#if ENGINE_VUFORIA
        static void Persist<T>(UnityEngine.Events.UnityEvent<T> value,UnityEngine.Events.UnityAction<T> callback)
        {
            for(int i=0;i<value.GetPersistentEventCount();i++)if(value.GetPersistentTarget(i)==(UnityEngine.Object)callback.Target && value.GetPersistentMethodName(i)==callback.Method.Name)return;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(value,callback);
        }
#endif
        public static void CreateBatch()
        {
            Create();
#if ENGINE_VUFORIA
            SessionState.SetBool("VuforiaSetup.Pending",false);
            Debug.Log("Vuforia SDK configured: delayed initialization="+Vuforia.VuforiaConfiguration.Instance.Vuforia.DelayedInitialization+", licence present="+!string.IsNullOrWhiteSpace(Vuforia.VuforiaConfiguration.Instance.Vuforia.LicenseKey));
            EditorApplication.Exit(0);
#else
            bool sdk=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.name=="com.ptc.vuforia.engine");
            if(sdk)SessionState.SetBool("VuforiaSetup.Pending",true);else EditorApplication.Exit(0);
#endif
        }
        [MenuItem("Tools/Engine Assembly/AR/Open Vuforia Licence Settings")]
        public static void OpenSettings()
        {
#if ENGINE_VUFORIA
            Selection.activeObject=Vuforia.VuforiaConfiguration.Instance;
#else
            Debug.LogWarning("Install the Vuforia Engine package before adding your app licence key.");
#endif
        }
    }
}
