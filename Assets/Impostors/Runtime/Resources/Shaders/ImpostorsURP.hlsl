#ifndef IMPOSTORS_URP_INCLUDED
#define IMPOSTORS_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#define IMPOSTORS_DECLARE_TEXTURE2D(name) TEXTURE2D(name); SAMPLER(sampler##name);
#define IMPOSTORS_SAMPLE_TEXTURE2D(name, uv) SAMPLE_TEXTURE2D(name, sampler##name, uv)

#include "ImpostorsCore.hlsl"

#if UNITY_VERSION >= 202120 // Unity above version 2021.2.1
    #define FOG_IN_FRAGMENT
#endif


struct ImpostorUrpVaryings
{
    float4 positionCS : SV_POSITION;
    // (z) - progress 0..1, (w) - side if (side == 0) {fading in} else {fading out}
    float4 uvFadeParams : TEXCOORD0;
    // required for cross fade
    float4 screenPos : TEXCOORD1;
    #ifdef FOG_IN_FRAGMENT
    float3 positionWS : TEXCOORD2;
    #else
    float fogFactor : TEXCOORD2;
    #endif
    float3 normalWS : TEXCOORD3;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ImpostorUrpVaryings ImpostorsUrpVertex(ImpostorAttributes input)
{
    ImpostorUrpVaryings output;
    ZERO_INITIALIZE(ImpostorUrpVaryings, output);
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    ImposterData data = UnpackImpostorAttributes(input);
    ImposterVertexOutput o = GetImposterVertexOutput(data);

    output.positionCS = TransformWorldToHClip(o.positionWS);
    output.screenPos = GetVertexPositionInputs(o.positionWS).positionNDC;
    
    #ifdef FOG_IN_FRAGMENT
    output.positionWS = o.positionWS;
    #else
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    #endif
    
    output.normalWS = o.directionWS;
    output.uvFadeParams = o.uvFadeParams;
    return output;
}

half4 ImpostorsUrpForwardFragment(ImpostorUrpVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    half4 color = IMPOSTORS_SAMPLE_TEXTURE2D(_MainTex, input.uvFadeParams.xy);
    color = ImpostorFragmentFunction(color, input.screenPos, _ScreenParams.xy, input.uvFadeParams.zw);

    #ifdef FOG_IN_FRAGMENT
    float fogFactor = InitializeInputDataFog(float4(input.positionWS, 1), 0);
    #else
    float fogFactor = input.fogFactor;
    #endif
    
    color.rgb = MixFog(color.rgb, fogFactor);
    return color + _ImpostorsDebugColor;
}

half ImpostorsUrpDepthOnlyFragment(ImpostorUrpVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 color = IMPOSTORS_SAMPLE_TEXTURE2D(_MainTex, input.uvFadeParams.xy);
    ImpostorFragmentFunction(color, input.screenPos, _ScreenParams.xy, input.uvFadeParams.zw);

    return input.positionCS.z;
}

void ImpostorsUrpDepthNormalsFragment(
    ImpostorUrpVaryings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 color = IMPOSTORS_SAMPLE_TEXTURE2D(_MainTex, input.uvFadeParams.xy);
    ImpostorFragmentFunction(color, input.screenPos, _ScreenParams.xy, input.uvFadeParams.zw);

    outNormalWS = half4(input.normalWS, 0);
    #ifdef _WRITE_RENDERING_LAYERS
        uint renderingLayers = GetMeshRenderingLayer();
        outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif
}

#endif
