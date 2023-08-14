#if IMPOSTORS_UNITY_PIPELINE_URP
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Impostors.URP
{
    public static class UrpUtility
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            dataProperty = typeof(UniversalRenderPipelineAsset)
                .GetProperty("scriptableRendererData", BindingFlags.NonPublic | BindingFlags.Instance);

            if (dataProperty == null)
            {
                Debug.LogError($"[IMPOSTORS] Can't find required property on {nameof(UniversalRenderPipelineAsset)}." +
                               $"Looks like Unity changed API.\n\n" +
                               $"Please, report a bug to Impostors developer. Unity version: '{Application.unityVersion}'\n\n");
            }
        }

        private static PropertyInfo dataProperty;

        public static bool TryGetCurrentImpostorsFeature(out UpdateImpostorsTexturesFeature feature)
        {
            return TryGetImpostorsFeature(GraphicsSettings.currentRenderPipeline, out feature);
        }

        public static bool TryGetImpostorsFeature(RenderPipelineAsset asset, out UpdateImpostorsTexturesFeature feature)
        {
            feature = null;
            var scriptableRendererData = GetScriptableRendererData(asset);
            if (scriptableRendererData == null || scriptableRendererData.rendererFeatures == null)
                return false;

            for (int i = 0; i < scriptableRendererData.rendererFeatures.Count; i++)
            {
                if (scriptableRendererData.rendererFeatures[i] is UpdateImpostorsTexturesFeature f)
                {
                    feature = f;
                    return true;
                }
            }

            return false;
        }

        public static ScriptableRendererData GetScriptableRendererData(RenderPipelineAsset asset)
        {
            var urpAsset = asset as UniversalRenderPipelineAsset;
            if (urpAsset == null)
                return null;
            if (dataProperty == null)
                return null;
            return dataProperty.GetValue(urpAsset) as ScriptableRendererData;
        }
    }
}
#endif