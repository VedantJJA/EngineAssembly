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
            Copy(SpatialFallbackSceneBuilder.AssemblyPath,AssemblyPath,"MobileAssemblyARFallback");
            Copy(SpatialFallbackSceneBuilder.DisplayPath,DisplayPath,"MobileDisplayARFallback");
            var paths=new[]{AssemblyPath,DisplayPath};
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>!paths.Contains(s.path)).Concat(paths.Select(p=>new EditorBuildSettingsScene(p,true))).ToArray();
            ConfigureSDK();AssetDatabase.SaveAssets();
        }
        static void Copy(string source,string destination,string fallback)
        {
            if(!File.Exists(destination) && !AssetDatabase.CopyAsset(source,destination))throw new IOException("Unable to copy "+source);
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(destination,OpenSceneMode.Additive);
            try
            {
                var host=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpatialExperienceRig>()).Single().gameObject;
                var provider=host.GetComponent<VuforiaGroundPlaneProvider>();if(!provider)provider=host.AddComponent<VuforiaGroundPlaneProvider>();
                var session=host.GetComponent<MobileARSession>();if(!session)session=host.AddComponent<MobileARSession>();
                session.Configure(provider,fallback);EditorSceneManager.SaveScene(scene);
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
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
