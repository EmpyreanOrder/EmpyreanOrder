#ifndef IMPOSTORS_CORE_INCLUDED
#define IMPOSTORS_CORE_INCLUDED

uniform float4 _ImpostorsTimeProvider;
uniform float _ImpostorsNoiseTextureResolution;
uniform float _ImpostorsCutoff;
uniform float _ImpostorsMinAngleToStopLookAt;
uniform float4 _ImpostorsDebugColor;
uniform float3 _ImpostorsWorldSpaceCameraPosition;

#define IMPOSTORS_WORLD_UP_VECTOR half3(0, 1, 0)

#ifndef IMPOSTORS_DECLARE_TEXTURE2D
    #error "Before including ImpostorsCore.hlsl you need to define IMPOSTORS_DECLARE_TEXTURE2D depending on render pipeline"
    #define IMPOSTORS_DECLARE_TEXTURE2D(name) sampler2D name
#endif

#ifndef IMPOSTORS_SAMPLE_TEXTURE2D
    #error "Before including ImpostorsCore.hlsl you need to define IMPOSTORS_SAMPLE_TEXTURE2D depending on render pipeline"
    #define IMPOSTORS_SAMPLE_TEXTURE2D(name, uv) tex2D(name, uv)
#endif

#ifndef UNITY_VERTEX_INPUT_INSTANCE_ID
    #error "Before including ImpostorsCore.hlsl you need to define UNITY_VERTEX_INPUT_INSTANCE_ID depending on render pipeline"
    #define UNITY_VERTEX_INPUT_INSTANCE_ID
#endif

IMPOSTORS_DECLARE_TEXTURE2D(_ImpostorsNoiseTexture);
IMPOSTORS_DECLARE_TEXTURE2D(_MainTex);

inline float invLerp(float from, float to, float value)
{
    return (value - from) / (to - from);
}

// TODO: the parameter called 'original' is always vectorUp. dot and length operations could be omitted.
inline float angleBetween(float3 v, float3 original)
{
    return acos(dot(v, original) / (length(v) * length(original)));
}

// Returns angle in radians between normalized input vector v and (0, 1, 0)
inline float angleToWorldUp(float3 v)
{
    return acos(v.y);
}

// side = 0 if fading in
// side = 1 if fading out 
inline void internal_ImpostorCrossFade(float4 screenPos, float2 screenResolution, float2 fadeParams)
{
    float progress = fadeParams.x;
    float side = fadeParams.y;
    float2 noiseUV = screenPos.xy / screenPos.w;
    noiseUV.x *= screenResolution.x / _ImpostorsNoiseTextureResolution;
    noiseUV.y *= screenResolution.y / _ImpostorsNoiseTextureResolution;
    float noiseValue = IMPOSTORS_SAMPLE_TEXTURE2D(_ImpostorsNoiseTexture, noiseUV).r;

    if (abs(side - noiseValue) > abs(side - progress))
    {
        discard;
    }
}

struct ImpostorAttributes
{
    // (x,y) - quad corner position, (z) - z offset. 
    float4 vertex : POSITION;
    // (x,y,z) - impostor's direction
    float3 normal : NORMAL;
    // (x,y) - uv, (w) - fade duration, (z) - not implemented, controls "always look at camera" 
    float4 texcoord : TEXCOORD0;
    // (x,y,z) - center of impostor, (w) - time when fade should end (if (w>0) then {fading in} else {fading out})
    float4 texcoord1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ImposterData
{
    float3 vertex;
    float3 centerWS;
    float3 lookDirectionWS;
    float2 uv;
    float fadeDuration;
    float fadeEndTime;
};

ImposterData UnpackImpostorAttributes(ImpostorAttributes v)
{
    ImposterData o;

    o.vertex = v.vertex.xyz;
    o.centerWS = v.texcoord1.xyz;
    o.lookDirectionWS = normalize(v.normal);
    o.uv = v.texcoord.xy;
    o.fadeDuration = v.texcoord.w;
    o.fadeEndTime = v.texcoord1.w;

    return o;
}

struct ImposterVertexOutput
{
    float3 positionWS;
    float3 directionWS;
    // (z) - progress 0..1, (w) - side if (side == 0) {fading in} else {fading out}
    float4 uvFadeParams;
};

inline float3 GetImpostorPlaneDirectionWS(float3 centerWS, float3 normalWS)
{
    float3 eyeVector = normalize(_ImpostorsWorldSpaceCameraPosition - centerWS);
    float deg = abs(degrees(angleToWorldUp(eyeVector)));
    if (deg < 1.25 * _ImpostorsMinAngleToStopLookAt)
    {
        float val = invLerp(_ImpostorsMinAngleToStopLookAt * 1.25, _ImpostorsMinAngleToStopLookAt, deg);
        eyeVector = lerp(normalWS, eyeVector, 1 - clamp(val, 0, 1));
        eyeVector = normalize(eyeVector);
    }

    return eyeVector;
}

inline float3 GetImpostorVertexPositionWS(float3 centerWS, float3 vertex, float3 normalWS)
{
    float3 right = cross(normalWS, IMPOSTORS_WORLD_UP_VECTOR);
    float3 up = cross(normalWS, right);

    up = normalize(up);
    right = normalize(right);

    float3 finalPosition = centerWS;
    finalPosition += vertex.x * right;
    finalPosition -= vertex.y * up;
    finalPosition += vertex.z * normalWS;

    return finalPosition;
}

inline float2 CalculateImpostorFadeParams(float targetTime, float fadeDuration, float currentTime, float3 normalWS,
                                          in out float3 positionWS)
{
    float timeDelta = abs(targetTime) - currentTime;
    float fadeProgress = 1;
    float side = -2;

    if (timeDelta > 0 && fadeDuration > 0.001)
    {
        fadeProgress = 1 - timeDelta / fadeDuration;
        fadeProgress = clamp(fadeProgress, 0, 1);
        // if fading in
        if (targetTime > 0)
        {
            side = 0;
            // add little position to get rid of z-fighting
            positionWS += normalWS * lerp(0.2, 0, fadeProgress);
        }
        // else, if fading out
        if (targetTime < 0)
        {
            side = 1;
            // add little position to get rid of z-fighting
            positionWS -= normalWS * lerp(0, 0.2, fadeProgress);
        }
    }
    else
    {
        // if faded out
        if (targetTime < 0)
        {
            side = 1;
            fadeProgress = 1;
        }
    }

    return float2(fadeProgress, side);
}

inline ImposterVertexOutput GetImposterVertexOutput(ImposterData data)
{
    ImposterVertexOutput o;
    o.directionWS = GetImpostorPlaneDirectionWS(data.centerWS, data.lookDirectionWS);

    o.positionWS = GetImpostorVertexPositionWS(data.centerWS, data.vertex, o.directionWS);

    o.uvFadeParams.zw = CalculateImpostorFadeParams(
        data.fadeEndTime,
        data.fadeDuration,
        _ImpostorsTimeProvider.x,
        o.directionWS,
        /*in out*/ o.positionWS
    );

    o.uvFadeParams.xy = data.uv;

    return o;
}

inline float4 ImpostorFragmentFunction(float4 textureColor, float4 screenPos, float2 screenResolution,
                                       float2 fadeParams)
{
    clip(textureColor.a - _ImpostorsCutoff);
    
    if (textureColor.a < 0.99 && textureColor.a > 0.01)
        textureColor.rgb /= textureColor.a;

    if (fadeParams.y > -1)
    {
        internal_ImpostorCrossFade(screenPos, screenResolution, fadeParams);

        #ifdef IMPOSTORS_DEBUG_FADING
        if (fadeParams.y == 0)
            textureColor *= lerp(1, float4(0,1,0,1), pow(1 - fadeParams.x, 0.4));
        else
            textureColor *= lerp(1, float4(1,0,0,1), pow(fadeParams.x, 0.4));
        #endif
    }

    return textureColor;
}
#endif
