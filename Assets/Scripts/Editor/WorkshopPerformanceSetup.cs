using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;

namespace EngineAssembly.Editor
{
    public static class WorkshopPerformanceSetup
    {
        [MenuItem("Tools/Engine Assembly/Performance/Apply mobile and Quest profiles")]
        public static void Configure()
        {
            var phone=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if(!phone)throw new System.InvalidOperationException("Mobile render pipeline is missing.");
            var quest=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Quest_RPAsset.asset");
            if(!quest){quest=Object.Instantiate(phone);quest.name="Quest_RPAsset";AssetDatabase.CreateAsset(quest,"Assets/Settings/Quest_RPAsset.asset");}
            SetProfile(phone,.8f,1,12);SetProfile(quest,.9f,2,12);
            // This profile is selected at runtime. Include its shader requirements
            // explicitly so build-time URP stripping cannot discard its XR variants.
            var inclusion=GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
            if(inclusion==null)throw new System.InvalidOperationException("URP build inclusion settings are missing.");
            inclusion.includeAssetsByLabel=true;
            if(string.IsNullOrEmpty(inclusion.labelToInclude))inclusion.labelToInclude="WorkshopRuntimePipelines";
            AssetDatabase.SetLabels(quest,AssetDatabase.GetLabels(quest).Concat(new[]{inclusion.labelToInclude}).Distinct().ToArray());
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"));
            var profiles=AssetDatabase.LoadAssetAtPath<WorkshopPerformanceProfiles>("Assets/Resources/WorkshopPerformanceProfiles.asset");
            if(!profiles){profiles=ScriptableObject.CreateInstance<WorkshopPerformanceProfiles>();AssetDatabase.CreateAsset(profiles,"Assets/Resources/WorkshopPerformanceProfiles.asset");}
            profiles.phone=phone;profiles.quest=quest;EditorUtility.SetDirty(profiles);
            PlayerSettings.Android.optimizedFramePacing=true;
            AssetDatabase.SaveAssets();
        }
        static void SetProfile(UniversalRenderPipelineAsset asset,float scale,int msaa,float shadows)
        {
            asset.supportsHDR=false;asset.renderScale=scale;asset.msaaSampleCount=msaa;asset.shadowDistance=shadows;
            var serialized=new SerializedObject(asset);
            foreach(string name in new[]{"m_ReflectionProbeBlending","m_ReflectionProbeBoxProjection","m_SupportsLightCookies","m_SupportsLightLayers","m_SupportDataDrivenLensFlare","m_SupportScreenSpaceLensFlare"})
                serialized.FindProperty(name).boolValue=false;
            serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=2;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);
        }
    }
}
