using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
namespace EngineAssembly.Editor
{
    public static class QuestWorkshopSetup
    {
        [MenuItem("Tools/Engine Assembly/VR/Configure Quest 2 and lift art")]
        public static void Configure()
        {
            WorkshopPerformanceSetup.Configure();
            Directory.CreateDirectory("Assets/XR/Settings");Directory.CreateDirectory("Assets/Resources/Lift");AssetDatabase.Refresh();
            var per=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/Settings/WorkshopXRSettings.asset");if(!per){per=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(per,"Assets/XR/Settings/WorkshopXRSettings.asset");}
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,per,true);
            foreach(var target in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
            {
                if(!per.HasSettingsForBuildTarget(target))per.CreateDefaultSettingsForBuildTarget(target);var general=per.SettingsForBuildTarget(target);general.InitManagerOnStart=false;
                if(!general.Manager){var manager=ScriptableObject.CreateInstance<XRManagerSettings>();manager.name=target+" XR manager";AssetDatabase.AddObjectToAsset(manager,per);general.Manager=manager;}
                general.Manager.automaticLoading=false;general.Manager.automaticRunning=false;
                if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target))throw new Exception("Unable to assign OpenXR loader for "+target);
                var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(target);var touch=settings.GetFeature<OculusTouchControllerProfile>();if(touch)touch.enabled=true;
                var quest=settings.GetFeature<MetaQuestFeature>();if(quest)quest.enabled=target==BuildTargetGroup.Android;
                var hands=settings.GetFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>();if(hands)hands.enabled=true;
                var aim=settings.GetFeature<UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim>();if(aim)aim.enabled=true;
                var foveation=settings.GetFeature<UnityEngine.XR.OpenXR.Features.FoveatedRenderingFeature>();
                if(foveation){foveation.enabled=target==BuildTargetGroup.Android;var config=new SerializedObject(foveation);config.FindProperty("useEyeTracking").boolValue=false;config.ApplyModifiedPropertiesWithoutUndo();}
                settings.foveatedRenderingApi=OpenXRSettings.BackendFovationApi.SRPFoveation;
                settings.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;EditorUtility.SetDirty(settings);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);
            }
            XRGeneralSettings.Instance=per.SettingsForBuildTarget(BuildTargetGroup.Standalone);EditorUtility.SetDirty(per);
            MakeLiftArt();var importer=AssetImporter.GetAtPath("Assets/Resources/Music/Contemplation.mp3") as AudioImporter;
            if(importer){var audio=importer.defaultSampleSettings;audio.loadType=AudioClipLoadType.Streaming;audio.compressionFormat=AudioCompressionFormat.Vorbis;audio.quality=.5f;audio.preloadAudioData=false;importer.defaultSampleSettings=audio;importer.SaveAndReimport();}
            AssetDatabase.SaveAssets();File.WriteAllText("Tools/Verification/quest-setup-report.json","{\"configured\":true,\"automaticXRStartup\":false,\"music\":\"Contemplation by Joth / CC0\"}");
        }
        [MenuItem("Tools/Engine Assembly/VR/Refit repair garage")]
        public static void RefitRepairGarage()
        {
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var source=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var repair=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RepairVRFallback.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var manager=repair.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AssemblyManager>()).First();
                foreach(var old in repair.GetRootGameObjects().Where(g=>g.name=="Garage environment" || g.name=="Garage workshop lighting"))UnityEngine.Object.DestroyImmediate(old);
                foreach(var root in source.GetRootGameObjects().Where(g=>g.name=="Garage environment" || g.name=="Garage workshop lighting"))
                {var copy=UnityEngine.Object.Instantiate(root);copy.name=root.name;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy,repair);}
                foreach(var old in repair.GetRootGameObjects().Where(g=>g.name=="Floor" || g.name=="Parts Table" || g.name=="Workshop Light"))
                {foreach(var r in old.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in old.GetComponentsInChildren<Collider>())c.enabled=false;if(old.TryGetComponent<Light>(out var light))light.enabled=false;}
                var sourceManager=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AssemblyManager>()).First();
                var supplies=manager.GetComponent<WorkshopSupplyLayout>();if(!supplies)supplies=manager.gameObject.AddComponent<WorkshopSupplyLayout>();EditorUtility.CopySerialized(sourceManager.GetComponent<WorkshopSupplyLayout>(),supplies);
                if(!manager.GetComponent<WorkpieceLift>())manager.gameObject.AddComponent<WorkpieceLift>();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(repair);
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(repair,true);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(source,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}
        }
        static void MakeLiftArt()
        {
            const string path="Assets/Resources/Lift/Treadplate.png";var texture=new Texture2D(256,256,TextureFormat.RGB24,true);var pixels=new Color[256*256];
            for(int y=0;y<256;y++)for(int x=0;x<256;x++){int tx=x%32,ty=y%32;bool stripe=((x/32+y/32)%2==0?Mathf.Abs(tx+ty-31):Mathf.Abs(tx-ty))<3 && tx>5 && tx<27 && ty>5 && ty<27;float c=stripe?.83f:.61f+(x%3)*.014f;pixels[y*256+x]=new Color(c,c,c);}
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);var map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material Create(string label,string shader){var asset="Assets/Resources/Lift/"+label+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(asset);if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,asset);}return m;}
            var steel=Create("Steel","Universal Render Pipeline/Lit");steel.SetColor("_BaseColor",new Color(.55f,.58f,.6f));steel.SetTexture("_BaseMap",map);steel.SetTextureScale("_BaseMap",new Vector2(2,2));steel.SetFloat("_Metallic",.75f);steel.SetFloat("_Smoothness",.35f);EditorUtility.SetDirty(steel);
            var pit=Create("Void","Universal Render Pipeline/Unlit");pit.SetColor("_BaseColor",Color.black);EditorUtility.SetDirty(pit);
        }
    }
}
