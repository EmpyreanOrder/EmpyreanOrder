using System;
using System.Collections.Generic;
using Impostors.Structs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Impostors
{
    public static class ImpostorsUtility
    {
        public static void CalculateFrustumPlanes(Camera cam, SimplePlane[] planes, float fovMult)
        {
            Matrix4x4 projMat = Matrix4x4.Perspective(cam.fieldOfView * fovMult,
                cam.aspect,
                cam.nearClipPlane,
                cam.farClipPlane);
            Matrix4x4 mat = projMat * cam.worldToCameraMatrix;

            // left
            planes[0].normal = new float3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02);
            planes[0].distance = mat.m33 + mat.m03;

            // right
            planes[1].normal = new float3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02);
            planes[1].distance = mat.m33 - mat.m03;

            // bottom
            planes[2].normal = new float3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12);
            planes[2].distance = mat.m33 + mat.m13;

            // top
            planes[3].normal = new float3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12);
            planes[3].distance = mat.m33 - mat.m13;

            // near
            planes[4].normal = new float3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22);
            planes[4].distance = mat.m33 + mat.m23;

            // far
            planes[5].normal = new float3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22);
            planes[5].distance = mat.m33 - mat.m23;

            // normalize
            for (uint i = 0; i < 6; i++)
            {
                float length = math.length(planes[i].normal);
                planes[i].normal /= length;
                planes[i].distance /= length;
            }
        }

        public static float MaxV3(Vector3 v)
        {
            return v.magnitude;
        }

        public static Color GetMainLightColorForShader(Light light, bool isUrp)
        {
            var lightColor = light.color;

            if (light.useColorTemperature)
            {
                var kelvinColor = Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
                lightColor *= kelvinColor;
            }

            if (isUrp)
            {
                lightColor = QualitySettings.activeColorSpace == ColorSpace.Gamma ? lightColor : lightColor.linear;
                lightColor *= light.intensity;
            }
            else
            {
                lightColor *= light.intensity;
                lightColor = QualitySettings.activeColorSpace == ColorSpace.Gamma ? lightColor : lightColor.linear;
            }

            return lightColor;
        }

        public static void SetFogShaderKeywordsEnabled(bool value, CommandBuffer cb)
        {
            if (!RenderSettings.fog)
                return;

            string keyword;
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    keyword = "FOG_LINEAR";
                    break;
                case FogMode.Exponential:
                    keyword = "FOG_EXP";
                    break;
                case FogMode.ExponentialSquared:
                    keyword = "FOG_EXP2";
                    break;
                default:
                    Debug.LogError($"Unknown FogMode: '{RenderSettings.fogMode}'");
                    keyword = "";
                    break;
            }

            if (value)
                cb.EnableShaderKeyword(keyword);
            else
                cb.DisableShaderKeyword(keyword);
        }


        internal static void DebugDrawBounds(Bounds b, Color color, float duration = 0)
        {
            // bottom
            var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
            var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
            var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
            var p4 = new Vector3(b.min.x, b.min.y, b.max.z);

            Debug.DrawLine(p1, p2, color, duration);
            Debug.DrawLine(p2, p3, color, duration);
            Debug.DrawLine(p3, p4, color, duration);
            Debug.DrawLine(p4, p1, color, duration);

            // top
            var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
            var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
            var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
            var p8 = new Vector3(b.min.x, b.max.y, b.max.z);

            Debug.DrawLine(p5, p6, color, duration);
            Debug.DrawLine(p6, p7, color, duration);
            Debug.DrawLine(p7, p8, color, duration);
            Debug.DrawLine(p8, p5, color, duration);

            // sides
            Debug.DrawLine(p1, p5, color, duration);
            Debug.DrawLine(p2, p6, color, duration);
            Debug.DrawLine(p3, p7, color, duration);
            Debug.DrawLine(p4, p8, color, duration);
        }

        public static class LightProbsUtility
        {
            private static bool IsInitialized { get; set; }
            private static List<Vector3> Positions;
            private static List<SphericalHarmonicsL2> LightProbes;

            private static void Initialize(int capacity)
            {
                if (IsInitialized)
                {
                    if (Positions.Capacity < capacity)
                    {
                        Positions.Capacity = capacity;
                        LightProbes.Capacity = capacity;
                    }

                    return;
                }

                IsInitialized = true;
                Positions = new List<Vector3>(capacity);
                LightProbes = new List<SphericalHarmonicsL2>(capacity);
            }

            public static List<SphericalHarmonicsL2> GetLightProbes(List<int> ids,
                NativeArray<SharedData> sharedDataArray)
            {
                Initialize(ids.Count);
                Positions.Clear();
                LightProbes.Clear();

                for (var i = 0; i < ids.Count; i++)
                {
                    var sharedData = sharedDataArray[ids[i]];
                    Positions.Add(sharedData.data.position);
                }

                UnityEngine.LightProbes.CalculateInterpolatedLightAndOcclusionProbes(Positions, LightProbes, null);

                return LightProbes;
            }

            [Obsolete]
            public static SphericalHarmonicsL2 CalculateTrilightAmbient(Color groundColor, Color equatorColor,
                Color skyColor)
            {
                // idk why linear is needed
                groundColor = groundColor.linear;
                equatorColor = equatorColor.linear;
                skyColor = skyColor.linear;
                // idk what that 0.55f multiplier is
                float scale1 = 1f / 7f * 0.55f;
                float scale2 = 2f / 7f * 0.55f;
                float scale3 = 3f / 7f * 0.55f;
                SphericalHarmonicsL2 sphericalHarmonics = new SphericalHarmonicsL2();
                sphericalHarmonics.AddDirectionalLight(Vector3.up, skyColor, scale3);
                sphericalHarmonics.AddDirectionalLight(Vector3.down, groundColor, scale3);

                // 0.61f instead of 0.5f
                Vector3 halfway = new Vector3(0.61237243569579f, 0.61f, 0.61237243569579f);

                // 0.77f instead of 0.5f
                var upperColor = Color.Lerp(skyColor, equatorColor, 0.77f);

                // upper
                sphericalHarmonics.AddDirectionalLight(new Vector3(-halfway.x, +halfway.y, -halfway.z), upperColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(+halfway.x, +halfway.y, -halfway.z), upperColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(-halfway.x, +halfway.y, +halfway.z), upperColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(+halfway.x, +halfway.y, +halfway.z), upperColor,
                    scale2);

                var centerColor = equatorColor;
                // center
                sphericalHarmonics.AddDirectionalLight(Vector3.left, centerColor, scale1);
                sphericalHarmonics.AddDirectionalLight(Vector3.right, centerColor, scale1);
                sphericalHarmonics.AddDirectionalLight(Vector3.forward, centerColor, scale1);
                sphericalHarmonics.AddDirectionalLight(Vector3.back, centerColor, scale1);

                // 0.77f instead of 0.5f
                var lowerColor = Color.Lerp(groundColor, equatorColor, 0.77f);
                // lower
                sphericalHarmonics.AddDirectionalLight(new Vector3(-halfway.x, -halfway.y, -halfway.z), lowerColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(+halfway.x, -halfway.y, -halfway.z), lowerColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(-halfway.x, -halfway.y, +halfway.z), lowerColor,
                    scale2);
                sphericalHarmonics.AddDirectionalLight(new Vector3(+halfway.x, -halfway.y, +halfway.z), lowerColor,
                    scale2);

                return sphericalHarmonics;
            }

            [Obsolete]
            public static SphericalHarmonicsL2 GetAmbientProbe()
            {
                SphericalHarmonicsL2 ambientProbe = default;
                switch (RenderSettings.ambientMode)
                {
                    case AmbientMode.Custom:
                    case AmbientMode.Skybox:
                        // 2.15f is empirical, I don't know why this value is used and why it requires power operation at all o_O
                        ambientProbe =
                            Mathf.Pow(RenderSettings.ambientIntensity, 2.15f) * RenderSettings.ambientProbe;
                        break;
                    case AmbientMode.Trilight:
                        ambientProbe = CalculateTrilightAmbient(RenderSettings.ambientGroundColor,
                            RenderSettings.ambientEquatorColor, RenderSettings.ambientSkyColor);
                        break;
                    case AmbientMode.Flat:
                        ambientProbe.AddAmbientLight(RenderSettings.ambientLight.linear);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(RenderSettings.ambientMode));
                }

                return ambientProbe;
            }
        }
    }
}