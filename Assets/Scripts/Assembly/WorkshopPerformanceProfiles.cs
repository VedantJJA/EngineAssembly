using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace EngineAssembly
{
    public sealed class WorkshopPerformanceProfiles:ScriptableObject
    {
        public UniversalRenderPipelineAsset phone,quest;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyDeviceProfile()
        {
            if(Application.platform!=RuntimePlatform.Android)return;
            bool headset=QuestVRSession.IsQuestModel(SystemInfo.deviceModel);
            var profiles=Resources.Load<WorkshopPerformanceProfiles>("WorkshopPerformanceProfiles");
            QualitySettings.SetQualityLevel(0,true);
            if(profiles)QualitySettings.renderPipeline=headset?profiles.quest:profiles.phone;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=headset?72:60;
            // Limit physics catch-up after a loading or thermal stall.
            Time.maximumDeltaTime=.1f;
        }
        public static void ConfigureXR(XRDisplaySubsystem display)
        {
            // Fixed foveation keeps the centre readable without requiring eye tracking.
            display.foveatedRenderingLevel=.5f;
            display.foveatedRenderingFlags=XRDisplaySubsystem.FoveatedRenderingFlags.None;
        }
    }
}
