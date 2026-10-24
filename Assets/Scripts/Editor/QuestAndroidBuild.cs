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
    public static class QuestAndroidBuild
    {
        public static bool BuildingQuest { get; private set; }
        [Serializable] sealed class Report{public bool passed;public string result,apk,error;public double seconds;public long bytes;}
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Tools/Verification/UnityProject/"))throw new Exception("Isolated build project only.");
            var report=new Report();try
            {
                BuildingQuest=true;
                QuestWorkshopSetup.Configure();PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                AssemblyAudioSetup.Run();
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.engineassembly.quest");EditorUserBuildSettings.buildAppBundle=false;
                report.apk=Path.GetFullPath("Builds/Quest/EngineAssembly-Quest2.apk");Directory.CreateDirectory(Path.GetDirectoryName(report.apk));
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),target=BuildTarget.Android,locationPathName=report.apk,options=BuildOptions.None});
                report.result=build.summary.result.ToString();report.passed=build.summary.result==BuildResult.Succeeded;report.seconds=build.summary.totalTime.TotalSeconds;report.bytes=File.Exists(report.apk)?new FileInfo(report.apk).Length:0;
                if(!report.passed)report.error=string.Join("\n",build.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error || m.type==LogType.Exception).Select(m=>m.content));
            }
            catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
            finally{BuildingQuest=false;PhoneAndroidBuildSettings.Configure();}
            File.WriteAllText("Tools/Verification/quest-android-build-report.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(report.passed?0:1);
        }
    }
}
