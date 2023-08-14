using System;
using Impostors.RenderPipelineProxy;
using UnityEngine;
#if IMPOSTORS_UNITY_PIPELINE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Impostors
{
    /// <summary>
    /// Responsible for providing appropriate type of RenderPipelineProxy for current project. 
    /// </summary>
    public static class RenderPipelineProxyTypeProvider
    {
        /// <summary>
        /// Returns appropriate type of RenderPipelineProxy for current project. 
        /// </summary>
        /// <returns></returns>
        public static Type Get()
        {
#if IMPOSTORS_UNITY_PIPELINE_URP
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
                return typeof(Impostors.URP.UniversalRenderPipelineProxy);
#endif
            return typeof(BuiltInRenderPipelineProxy);
        }

        /// <summary>
        /// Returns true if provided type is one of the types that could be provided by <see cref="RenderPipelineProxyTypeProvider"/>.
        /// </summary>
        /// <param name="type">RenderPipelineProxy type</param>
        public static bool IsOneOfStandardProxy(Type type)
        {
            if (typeof(BuiltInRenderPipelineProxy) == type)
                return true;
#if IMPOSTORS_UNITY_PIPELINE_URP
            if (typeof(Impostors.URP.UniversalRenderPipelineProxy) == type)
                return true;
#endif
            return false;
        }
    }
}