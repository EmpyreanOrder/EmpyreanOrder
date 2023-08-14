using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Impostors.RenderPipelineProxy
{
    public class BuiltInRenderPipelineProxy : RenderPipelineProxyBase
    {
        private const CameraEvent ForwardRenderingCameraEvent = CameraEvent.BeforeForwardOpaque;
        private const CameraEvent DeferredRenderingCameraEvent = CameraEvent.BeforeGBuffer; // todo test

        [FormerlySerializedAs("ImpostorRenderingType")]
        public ImpostorTextureUpdateMode ImpostorUpdateMode = ImpostorTextureUpdateMode.Scheduled;

        private CommandBuffer _previousCommandBuffer;
        private Camera _previousCamera;
        private CameraEvent? _previousCameraEvent;

        public enum ImpostorTextureUpdateMode
        {
            Scheduled,
            Immediate
        }

        private void Update()
        {
#if UNITY_EDITOR // this allows to inspect impostor's texture rendering through Frame Debugger
            if (Application.isEditor && UnityEditor.EditorApplication.isPaused)
                return;
#endif
            ClearPreviousCommandBuffer();
        }

        protected override void SubscribeToOnPreCull()
        {
            Camera.onPreCull += OnPreCullCalled;
            Camera.onPostRender += OnPostRenderCalled;
        }

        protected override void UnsubscribeFromOnPreCull()
        {
            Camera.onPreCull -= OnPreCullCalled;
            Camera.onPostRender -= OnPostRenderCalled;
        }

        public override void SetupMainLight(Light light, CommandBufferProxy bufferProxy)
        {
            var bakeType = light.bakingOutput.lightmapBakeType;
            var cb = bufferProxy.CommandBuffer;

            if (bakeType == LightmapBakeType.Mixed)
                bufferProxy.EnableShaderKeyword(ShaderKeywords.SHADOWS_SHADOWMASK);

            Vector4 lightPos = light.transform.localToWorldMatrix.GetColumn(2);
            var lightDir = new Vector4(-lightPos.x, -lightPos.y, -lightPos.z, 0).normalized;

            var lightColor = ImpostorsUtility.GetMainLightColorForShader(light, false);
            if (bakeType == LightmapBakeType.Baked)
                lightColor *= 0;

            cb.SetGlobalVector(ShaderProperties._WorldSpaceLightPos0, lightDir);
            cb.SetGlobalVector(ShaderProperties._LightColor0, lightColor);
        }

        public override void ScheduleImpostorTextureRendering(CommandBuffer commandBuffer, Camera camera)
        {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            ClearPreviousCommandBuffer();

            switch (ImpostorUpdateMode)
            {
                case ImpostorTextureUpdateMode.Scheduled:
                    var cameraEvent = GetCameraEvent(camera);
                    camera.AddCommandBuffer(cameraEvent, commandBuffer);
                    _previousCommandBuffer = commandBuffer;
                    _previousCamera = camera;
                    _previousCameraEvent = cameraEvent;
                    break;
                case ImpostorTextureUpdateMode.Immediate:
                    Graphics.ExecuteCommandBuffer(commandBuffer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ImpostorUpdateMode));
            }
        }

        public override void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer,
            Camera camera,
            int submeshIndex, MaterialPropertyBlock materialPropertyBlock, bool castShadows, bool receiveShadows,
            bool useLightProbes)
        {
            Graphics.DrawMesh(mesh, position, rotation, material, layer, camera, submeshIndex, materialPropertyBlock,
                castShadows, receiveShadows, useLightProbes);
        }

        [ContextMenu("Clear Previous Command Buffer")]
        private void ClearPreviousCommandBuffer()
        {
            if (_previousCommandBuffer != null && _previousCamera != null && _previousCameraEvent != null)
            {
                _previousCamera.RemoveCommandBuffer(_previousCameraEvent.Value, _previousCommandBuffer);
                _previousCommandBuffer = null;
                _previousCamera = null;
                _previousCameraEvent = null;
            }
        }

        private static CameraEvent GetCameraEvent(Camera camera)
        {
            switch (camera.actualRenderingPath)
            {
                case RenderingPath.Forward:
                    return ForwardRenderingCameraEvent;
                case RenderingPath.DeferredShading:
                    return DeferredRenderingCameraEvent;
                default:
                    Debug.LogError(
                        $"Unsupported rendering path: '{camera.actualRenderingPath}'. " +
                        $"Either change rendering path or provide custom {nameof(RenderPipelineProxy)}");
                    return ForwardRenderingCameraEvent;
            }
        }
    }
}