//UNITY_SHADER_NO_UPGRADE
#ifndef TOD_WEATHER_GLOBALS_INCLUDED
#define TOD_WEATHER_GLOBALS_INCLUDED

//Rain Data
float4 _PW_RainDataA;
float4 _PW_RainDataB;
Texture2D _PW_RainMap;

//Sand Data
float4 _PW_SandDataA;
Texture2D _PW_SandAlbedoMap;
Texture2D _PW_SandNormalMap;
Texture2D _PW_SandMaskMap;

//Snow Data
#if !defined(_SNOW_ON)
float4 _PW_SnowDataA;
float4 _PW_SnowDataB;
Texture2D _PW_SnowAlbedoMap;
Texture2D _PW_SnowNormalMap;
Texture2D _PW_SnowMaskMap;
float4 _PW_SnowColor;
#endif

float4 _WorldSpaceLightPos0;

void getRainGlobals_float(out float4 rainDataA, out float4 rainDataB)
{
	rainDataA = _PW_RainDataA;
	rainDataB = _PW_RainDataB;
}


//Sand Data
void getSandGlobals_float(out float4 sandDataA)
{
	sandDataA = _PW_SandDataA;
}

void getSandMaterial_float(in float3 worldPosition, SamplerState samp, out float4 sandAlbedo, out float3 sandNormal, out float4 sandMask)
{
	float2 sandUVs = worldPosition.xz / _PW_SandDataA.a;

	sandAlbedo = SAMPLE_TEXTURE2D(_PW_SandAlbedoMap, samp, sandUVs);
	sandNormal = UnpackNormal(SAMPLE_TEXTURE2D(_PW_SandNormalMap, samp, sandUVs));
	sandMask = SAMPLE_TEXTURE2D(_PW_SandMaskMap, samp, sandUVs);
}

//Snow Data
void getSnowGlobals_float(out float4 snowDataA, out float4 snowDataB)
{
	snowDataA = _PW_SnowDataA;
	snowDataB = _PW_SnowDataB;
}

void getSnowMaterial_float(in float3 worldPosition, SamplerState samp, out float4 snowAlbedo, out float3 snowNormal, out float4 snowMask)
{
	float2 snowUVs = worldPosition.xz / _PW_SnowDataB.r;

	snowAlbedo = SAMPLE_TEXTURE2D(_PW_SnowAlbedoMap, samp, snowUVs) * _PW_SnowColor;
	snowNormal = UnpackNormal(SAMPLE_TEXTURE2D(_PW_SnowNormalMap, samp, snowUVs));
	snowMask = SAMPLE_TEXTURE2D(_PW_SnowMaskMap, samp, snowUVs);
}


float generateRain(float pointSample, float speed, float offset)
{
	float time = frac((_Time.y * speed) + offset);
	float rainGen = sin(lerp(0, -20, time) * pointSample);
	float timeMask = sin(time * 3.1415);

	return saturate(rainGen * timeMask);
}

float sampleRain(float2 uv, float4 pointData)
{
	float rainA = generateRain(pointData.r, _PW_RainDataB.r, 0);
	float rainB = generateRain(pointData.g, _PW_RainDataB.r, 0.5);
	float rainC = generateRain(pointData.b, _PW_RainDataB.r, 0.75);
	float rainD = generateRain(pointData.a, _PW_RainDataB.r, 0.25);

	return saturate(rainA + rainB + rainC + rainD);
}

void getRainNormals_float(in float3 worldPosition, SamplerState samp, out float3 rainNormal)
{	
	
	float2 rainCenterUVs = worldPosition.xz / _PW_RainDataB.a;
	float4 centerRainMap = SAMPLE_TEXTURE2D(_PW_RainMap, samp, rainCenterUVs);

	float2 rainXDerivUVs = (worldPosition.xz / _PW_RainDataB.a) + float2(0.001, 0);
	float4 xDerivRainMap = SAMPLE_TEXTURE2D(_PW_RainMap, samp, rainXDerivUVs);

    float2 rainYDerivUVs = (worldPosition.xz / _PW_RainDataB.a) + float2(0, 0.001);
	float4 yDerivRainMap = SAMPLE_TEXTURE2D(_PW_RainMap, samp, rainYDerivUVs);

	//Sample center rain
	float centerRain = sampleRain(rainCenterUVs, centerRainMap);
	float xDerivRain = sampleRain(rainXDerivUVs, xDerivRainMap);
	float yDerivRain = sampleRain(rainYDerivUVs, yDerivRainMap);

	float xDeriv = centerRain - xDerivRain;
	float yDeriv = centerRain - yDerivRain;

	float3 xVector = float3(xDeriv, 1, 0);
	float3 yVector = float3(0, 1, yDeriv);

	rainNormal =  cross(xVector, yVector);

}
#endif