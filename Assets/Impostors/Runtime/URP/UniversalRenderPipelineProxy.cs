#if IMPOSTORS_UNITY_PIPELINE_URP
using Impostors.RenderPipelineProxy;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Impostors.URP
{
    public class UniversalRenderPipelineProxy : RenderPipelineProxyBase
    {
        [FormerlySerializedAs("ImpostorRenderingType")]
        public ImpostorTextureUpdateMode ImpostorUpdateMode = ImpostorTextureUpdateMode.Immediate;

        private RenderPipelineAsset _lastRenderPipelineAsset;
        private UpdateImpostorsTexturesFeature _lastUpdateFeature;

        public enum ImpostorTextureUpdateMode
        {
            Scheduled,
            Immediate
        }

        protected override void SubscribeToOnPreCull()
        {
            RenderPipelineManager.beginCameraRendering += RenderPipelineManagerOnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += RenderPipelineManagerOnEndCameraRendering;
        }

        protected override void UnsubscribeFromOnPreCull()
        {
            RenderPipelineManager.beginCameraRendering -= RenderPipelineManagerOnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= RenderPipelineManagerOnEndCameraRendering;
        }

        private void RenderPipelineManagerOnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnPreCullCalled(camera);
        }

        private void RenderPipelineManagerOnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnPostRenderCalled(camera);
        }

        public override void ScheduleImpostorTextureRendering(CommandBuffer commandBuffer, Camera camera)
        {
            if (_lastUpdateFeature != null)
                _lastUpdateFeature.Clear(camera);

            if (ImpostorUpdateMode == ImpostorTextureUpdateMode.Immediate)
            {
                _lastUpdateFeature = null;
                _lastRenderPipelineAsset = null;
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return;
            }

            if (_lastRenderPipelineAsset != GraphicsSettings.currentRenderPipeline)
            {
                if (UrpUtility.TryGetImpostorsFeature(GraphicsSettings.currentRenderPipeline, out var feature) == false)
                {
                    Debug.LogError(
                        $"[IMPOSTORS] Current render pipeline asset is not configured to work with {nameof(UpdateImpostorsTexturesFeature)}. " +
                        $"Refere to documentation on how to setup Impostors for URP.\n" +
                        $"Current render pipeline asset: '{(GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "NULL")}'\n\n" +
                        $"Falling back to {nameof(ImpostorTextureUpdateMode.Immediate)} mode...\n\n",
                        GraphicsSettings.currentRenderPipeline);
                }

                _lastUpdateFeature = feature;
                _lastRenderPipelineAsset = GraphicsSettings.currentRenderPipeline;
            }

            if (_lastUpdateFeature != null)
                _lastUpdateFeature.AddCommandBuffer(camera, commandBuffer);
            else
                Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public override void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer,
            Camera camera,
            int submeshIndex, MaterialPropertyBlock materialPropertyBlock, bool castShadows, bool receiveShadows,
            bool useLightProbes)
        {
            Graphics.DrawMesh(mesh, position, rotation, material, layer, camera, submeshIndex, materialPropertyBlock,
                castShadows, receiveShadows, useLightProbes);
        }

        public override float GetHeightRenderScale()
        {
            var urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
                return 1f;
            return base.GetHeightRenderScale() * urpAsset.renderScale;
        }

        public override void SetupMainLight(Light light, CommandBufferProxy bufferProxy)
        {
            var bakeType = light.bakingOutput.lightmapBakeType;
            var cb = bufferProxy.CommandBuffer;

            if (bakeType == LightmapBakeType.Mixed)
                bufferProxy.EnableShaderKeyword(ShaderKeywords.SHADOWS_SHADOWMASK);

            Vector4 lightPos = light.transform.localToWorldMatrix.GetColumn(2);
            var lightDir = new Vector4(-lightPos.x, -lightPos.y, -lightPos.z, 0).normalized;

            var lightColor = ImpostorsUtility.GetMainLightColorForShader(light, true);
            if (bakeType == LightmapBakeType.Baked)
                lightColor *= 0;

            cb.SetGlobalVector(ShaderProperties._MainLightPosition, lightDir);
            cb.SetGlobalVector(ShaderProperties._MainLightColor, lightColor);
        }
    }
}
#endif