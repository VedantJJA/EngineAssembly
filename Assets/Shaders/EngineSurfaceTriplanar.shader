Shader "EngineAssembly/Surface Triplanar"
{
    Properties
    {
        _BaseColor("Surface colour", Color) = (0.65,0.68,0.72,1)
        _BaseMap("Surface texture", 2D) = "white" {}
        _RoughnessMap("Roughness", 2D) = "white" {}
        _DetailNormal("OpenGL detail normal", 2D) = "bump" {}
        _TextureScale("Object-space texture density", Float) = 3200
        _TextureStrength("Texture variation", Range(0,1)) = 0.15
        _NormalStrength("Detail normal strength", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0.7
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TextureScale, _TextureStrength, _NormalStrength, _Metallic, _Smoothness;
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
                float3 positionOS:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                float3 normalOS:TEXCOORD2;
                half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS;
                output.positionWS=position.positionWS;
                output.positionOS=input.positionOS.xyz;
                output.normalOS=input.normalOS;
                output.fog=ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalOS);
                float3 weights=pow(abs(normal),4);weights/=max(dot(weights,1),.0001);
                float3 signs=step(0,normal)*2-1;
                float3 p=input.positionOS*_TextureScale;
                float2 uvX=float2(-signs.x*p.z,p.y);
                float2 uvY=float2(-signs.y*p.x,p.z);
                float2 uvZ=float2(signs.z*p.x,p.y);
                half3 colour=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvX).rgb*weights.x
                            +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvY).rgb*weights.y
                            +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvZ).rgb*weights.z;
                half roughness=SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,uvX).r*weights.x
                              +SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,uvY).r*weights.y
                              +SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,uvZ).r*weights.z;
                float2 nx=SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,uvX).rg*2-1;
                float2 ny=SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,uvY).rg*2-1;
                float2 nz=SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,uvZ).rg*2-1;
                float3 detail=float3(0,nx.y,-signs.x*nx.x)*weights.x
                             +float3(-signs.y*ny.x,0,ny.y)*weights.y
                             +float3(signs.z*nz.x,nz.y,0)*weights.z;
                normal=normalize(normal+detail*_NormalStrength);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb*lerp(1,colour,_TextureStrength);
                surface.metallic=_Metallic;
                surface.smoothness=saturate(_Smoothness+(0.5-roughness)*.24);
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;surface.alpha=1;
                InputData data=(InputData)0;
                data.positionWS=input.positionWS;
                data.normalWS=NormalizeNormalPerPixel(TransformObjectToWorldNormal(normal));
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(data.normalWS);
                data.shadowMask=half4(1,1,1,1);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                data.fogCoord=input.fog;
                half4 result=UniversalFragmentPBR(data,surface);
                result.rgb=MixFog(result.rgb,input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
