using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace EngineAssembly.Editor
{
    // Disabling automatic XR startup alone does not stop native pre-initialization.
    // XR Management writes xrsdk-pre-init-library whenever a loader is assigned.
    public sealed class PhoneAndroidBuildSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android && !QuestAndroidBuild.BuildingQuest)
                Configure();
        }

        [MenuItem("Tools/Engine Assembly/Android/Configure Phone (AR and standard view)")]
        public static void Configure()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general)
            {
                general.InitManagerOnStart = false;
                if (general.Manager)
                {
                    general.Manager.automaticLoading = false;
                    general.Manager.automaticRunning = false;
                    general.Manager.TrySetLoaders(new List<XRLoader>());
                    EditorUtility.SetDirty(general.Manager);
                }
                EditorUtility.SetDirty(general);
            }
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR)
            {
                foreach (var feature in openXR.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                {
                    // Both the current and legacy Quest features can alter the launcher.
                    if (feature && (feature.GetType().Name == "MetaQuestFeature" || feature.GetType().Name == "OculusQuestFeature"))
                        feature.enabled = false;
                }
                EditorUtility.SetDirty(openXR);
            }
            // Remove native VR boot flags left by a previous headset build.
            const string key = "xr-boot-settings";
            string platform = BuildPipeline.GetBuildTargetName(BuildTarget.Android);
            string boot = EditorUserBuildSettings.GetPlatformSettings(platform, key) ?? "";
            var retained = boot.Split(';').Where(s => !s.StartsWith("xrsdk-pre-init-library:") && !s.StartsWith("xr-"));
            EditorUserBuildSettings.SetPlatformSettings(platform, key, string.Join(";", retained));
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.engineassembly.app");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
        }
    }
}
