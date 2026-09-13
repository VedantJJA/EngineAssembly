Shader "EngineAssembly/GhostHologramURP"
{
    Properties
    {
        [Header(Ghost Hologram Colors)]
        _BaseColor ("Base Color", Color) = (0.15, 0.6, 1.0, 0.4)
        _RimColor ("Rim Glow Color", Color) = (0.3, 0.85, 1.0, 0.7)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.2
        
        [Header(Animation and Alpha)]
        _AlphaMultiplier ("Overall Alpha", Range(0.0, 1.0)) = 0.85
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.5
        _PulseIntensity ("Pulse Intensity", Range(0.0, 1.0)) = 0.25
        
        [Header(Rendering Options)]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 0 // Off (Double-sided)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8 // Always (Visible through engine block)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull [_CullMode]

        Pass
        {
            Name "GhostHologramPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _AlphaMultiplier;
                float _PulseSpeed;
                float _PulseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Fresnel / Rim calculation
                float rimPow = (_RimPower > 0.1) ? _RimPower : 2.5;
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, rimPow);

                // Gentle pulsing animation
                float pulse = 1.0;
                if (_PulseSpeed > 0.0)
                {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;
                }

                // Default alpha multiplier if uninitialized
                float alphaMult = (_AlphaMultiplier > 0.01) ? _AlphaMultiplier : 0.65;

                // Combine base color and rim glow
                float3 finalColor = lerp(_BaseColor.rgb, _RimColor.rgb, fresnel) * pulse;
                float baseA = (_BaseColor.a > 0.01) ? _BaseColor.a : 0.4;
                float alpha = saturate((baseA + fresnel * 0.4) * alphaMult * pulse);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
