Shader "EngineAssembly/UI/Inverting Crosshair"
{
    Properties { _StencilComp("Stencil comparison",Float)=8 _Stencil("Stencil",Float)=0 _StencilOp("Stencil operation",Float)=0 _StencilWriteMask("Stencil write mask",Float)=255 _StencilReadMask("Stencil read mask",Float)=255 _ColorMask("Color mask",Float)=15 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode] ColorMask [_ColorMask]
        Blend OneMinusDstColor Zero, Zero One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;}
            half4 frag(Varyings input):SV_Target{return half4(1,1,1,1);}
            ENDHLSL
        }
    }
}
