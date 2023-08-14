#ifndef IMPOSTORS_BIRP_INCLUDED
#define IMPOSTORS_BIRP_INCLUDED

#include "UnityCG.cginc"

#define IMPOSTORS_DECLARE_TEXTURE2D(name) sampler2D name
#define IMPOSTORS_SAMPLE_TEXTURE2D(name, uv) tex2D(name, uv)

#include "ImpostorsCore.hlsl"


struct ImpostorBirpVaryings
{
    float4 positionCS : SV_POSITION;
    // (z) - progress 0..1, (w) - side if (side == 0) {fading in} else {fading out}
    float4 uvFadeParams : TEXCOORD0;
    // required for cross fade
    float4 screenPos : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    UNITY_FOG_COORDS(3)
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ImpostorBirpVaryings ImpostorsBirpVertex(ImpostorAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    ImpostorBirpVaryings output;
    UNITY_INITIALIZE_OUTPUT(ImpostorBirpVaryings, output);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    ImposterData data = UnpackImpostorAttributes(input);
    ImposterVertexOutput o = GetImposterVertexOutput(data);

    output.positionCS = UnityWorldToClipPos(o.positionWS);
    output.screenPos = ComputeScreenPos(output.positionCS);
    output.normalWS = o.directionWS;
    output.uvFadeParams = o.uvFadeParams;
    UNITY_TRANSFER_FOG(output, output.positionCS);
    return output;
}

half4 ImpostorsBirpForwardFragment(ImpostorBirpVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    half4 color = tex2D(_MainTex, input.uvFadeParams.xy);
    color = ImpostorFragmentFunction(color, input.screenPos, _ScreenParams.xy, input.uvFadeParams.zw);

    UNITY_APPLY_FOG(input.fogCoord, color);
    return color + _ImpostorsDebugColor;
}

void ImpostorsBirpDeferredFragment(
    ImpostorBirpVaryings input,
    out half4 outDiffuse : SV_Target0, // RT0: diffuse color (rgb), occlusion (a)
    out half4 outSpecSmoothness : SV_Target1, // RT1: spec color (rgb), smoothness (a)
    out half4 outNormal : SV_Target2, // RT2: normal (rgb), --unused, very low precision-- (a) 
    out half4 outEmission : SV_Target3
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 color = ImpostorsBirpForwardFragment(input);

    // diffuse should be black, because it is affected by lights and then combined with emissions, which we don't want to. 
    outDiffuse = half4(0, 0, 0, 0);

    // idk, black seems to work here
    outSpecSmoothness = half4(0, 0, 0, 0);

    // set normal to zero so that lights do not affect impostors
    outNormal = half4(0, 0, 0, 1);

    // using emission buffer to output true colors of the impostor texture without lighting
    outEmission = color;
    #ifndef UNITY_HDR_ON
    outEmission.rgb = exp2(-outEmission.rgb);
    #endif
    outEmission += _ImpostorsDebugColor;
}

#endif
