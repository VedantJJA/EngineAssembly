using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class VuforiaAndroidBuild
    {
        static VuforiaAndroidBuild()
        {
            if(SessionState.GetBool("VuforiaAndroidBuild.Pending",false))EditorApplication.delayCall+=RunBatch;
        }
        [Serializable] sealed class Report
        {
            public bool passed,licencePresent,agreementAccepted;
            public string result,apk,error;
            public double seconds;
            public long bytes;
        }
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Tools/Verification/UnityProject/"))
                throw new InvalidOperationException("Build verification runs in the isolated project only.");
#if !ENGINE_VUFORIA
            if(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.name=="com.ptc.vuforia.engine"))
            {
                SessionState.SetBool("VuforiaAndroidBuild.Pending",true);VuforiaSceneSetup.ConfigureSDK();return;
            }
#endif
            SessionState.SetBool("VuforiaAndroidBuild.Pending",false);
            var report=new Report();
            try
            {
#if ENGINE_VUFORIA
                report.licencePresent=!string.IsNullOrWhiteSpace(Vuforia.VuforiaConfiguration.Instance.Vuforia.LicenseKey);
                var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance;
                var type=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Vuforia.Unity.Editor").GetType("Vuforia.EditorClasses.EulaVersioningUtility");
                var utility=type.GetMethod("CreateDefaultEulEulaVersioningUtility",flags).Invoke(null,null);
                report.agreementAccepted=(bool)type.GetMethod("HasUserAcceptedCurrentVersionEula",flags).Invoke(utility,null);
                if(!report.licencePresent || !report.agreementAccepted)throw new InvalidOperationException("App licence and Vuforia agreement must be configured before building.");
                VuforiaSceneSetup.ConfigureSDK();VuforiaSceneSetup.Create();
                PhoneAndroidBuildSettings.Configure();
                AssemblyAudioSetup.Run();
                // Phone builds must keep their normal launcher and AR camera.
                var phoneXR=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                var questFeature=phoneXR?phoneXR.GetFeature<UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature>():null;if(questFeature)questFeature.enabled=false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.engineassembly.app");
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;PlayerSettings.allowedAutorotateToPortrait=true;PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
                EditorUserBuildSettings.buildAppBundle=false;
                // Keep all menu destinations and both phone fallbacks available.
                var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                report.apk=Path.GetFullPath("Builds/Android/EngineAssembly-AR.apk");Directory.CreateDirectory(Path.GetDirectoryName(report.apk));
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=report.apk,target=BuildTarget.Android,options=BuildOptions.None});
                report.result=build.summary.result.ToString();report.seconds=build.summary.totalTime.TotalSeconds;report.bytes=File.Exists(report.apk)?new FileInfo(report.apk).Length:0;report.passed=build.summary.result==BuildResult.Succeeded;
                if(!report.passed)report.error=string.Join("\n",build.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error || m.type==LogType.Exception).Select(m=>m.content));
#else
                throw new InvalidOperationException("Vuforia SDK is not active in the Android compilation target.");
#endif
            }
            catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/android-ar-build-report.json",JsonUtility.ToJson(report,true));
            EditorApplication.Exit(report.passed?0:1);
        }
    }
}
