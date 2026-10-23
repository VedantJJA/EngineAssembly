#ifndef ENGINE_DISPLAY_SECTION_INCLUDED
#define ENGINE_DISPLAY_SECTION_INCLUDED
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _TextureScale, _TextureStrength, _NormalStrength, _Metallic, _Smoothness;
    float _ContextOpacity, _SectionEnabled, _SectionEdgeWidth, _Cull, _Cutoff;
    float4 _SectionPlane, _SectionColor;
    float _CamPoseWarp;
    float4 _CamAxisStations, _CamPhaseOffsets;
    float4x4 _EngineToWorld, _WorldToEngine;
CBUFFER_END
void EngineDisplayPose(inout float3 positionOS,inout float3 normalOS)
{
    if(_CamPoseWarp<.5)return;
    float3 p=mul(_WorldToEngine,float4(TransformObjectToWorld(positionOS),1)).xyz;
    float3 n=mul((float3x3)_WorldToEngine,TransformObjectToWorldNormal(normalOS));
    float angle=p.z<_CamAxisStations.z?_CamPhaseOffsets.x:p.z<_CamAxisStations.w?_CamPhaseOffsets.y:_CamPhaseOffsets.z;
    float s,c;sincos(radians(angle),s,c);float2 delta=p.xy-_CamAxisStations.xy;
    p.xy=_CamAxisStations.xy+float2(c*delta.x-s*delta.y,s*delta.x+c*delta.y);
    n.xy=float2(c*n.x-s*n.y,s*n.x+c*n.y);
    positionOS=TransformWorldToObject(mul(_EngineToWorld,float4(p,1)).xyz);
    normalOS=TransformWorldToObjectDir(mul((float3x3)_EngineToWorld,n),true);
}
void EngineDisplayClip(float3 positionWS, float2 pixel)
{
    if (_SectionEnabled > .5) clip(-dot(float4(positionWS,1),_SectionPlane));
    // Screen-door transparency keeps the full casing silhouette and works with depth writes.
    if (_ContextOpacity < .999)
    {
        float noise=frac(52.9829189*frac(dot(floor(pixel),float2(.06711056,.00583715))));
        clip(_ContextOpacity-noise);
    }
}
#endif
