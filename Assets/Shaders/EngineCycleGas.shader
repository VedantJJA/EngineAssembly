Shader "EngineAssembly/Cycle Gas"
{
    Properties { _BaseColor("Gas colour",Color)=(1,.3,.05,.2) _Heat("Combustion",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor;float _Heat; CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 positionWS:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionOS=i.positionOS.xyz;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float rim=pow(1-saturate(abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)))),2);
                float flame=.5+.5*sin(i.positionOS.x*28+i.positionOS.z*23+i.positionOS.y*17-_Time.y*19);
                half3 colour=lerp(_BaseColor.rgb,half3(1,.85,.28),_Heat*flame);
                return half4(colour*(1+_Heat*.7),saturate(_BaseColor.a+rim*.12+_Heat*flame*.16));
            }
            ENDHLSL
        }
    }
}
