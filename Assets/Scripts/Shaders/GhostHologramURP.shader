Shader "EngineAssembly/GhostHologramURP"
{
    Properties
    {
        _BaseColor("Fill",Color)=(0.2,0.65,1,0.22)
        _RimColor("Edge",Color)=(0.2,0.8,1,0.8)
        _RimPower("Edge falloff",Range(0.5,8))=2.5
        _PulseSpeed("Pulse speed",Float)=2
        _PulseIntensity("Pulse intensity",Range(0,1))=0.08
        _AlphaMultiplier("Opacity",Range(0,1))=1
        _SurfaceOffset("Surface offset",Float)=0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test",Float)=4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "AssemblyPreview"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower, _PulseSpeed, _PulseIntensity, _AlphaMultiplier, _SurfaceOffset;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz)+output.normalWS*_SurfaceOffset;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half rim=pow(1-saturate(abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),_RimPower);
                half pulse=1+sin(_Time.y*_PulseSpeed)*_PulseIntensity;
                half alpha=saturate(lerp(_BaseColor.a,_RimColor.a,rim)*_AlphaMultiplier*pulse);
                return half4(lerp(_BaseColor.rgb,_RimColor.rgb,rim)*pulse,alpha);
            }
            ENDHLSL
        }
    }
}

