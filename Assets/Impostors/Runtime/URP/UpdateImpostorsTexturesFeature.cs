#if IMPOSTORS_UNITY_PIPELINE_URP
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEngine.Experimental.Rendering;
#endif
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Impostors.URP
{
    public class UpdateImpostorsTexturesFeature : ScriptableRendererFeature
    {
        private Dictionary<Camera, UpdateImpostorsTexturesRenderPass> _renderPasses;

        [SerializeField]
        private bool _clearBufferAfterPass = true;

        public override void Create()
        {
            _renderPasses = new Dictionary<Camera, UpdateImpostorsTexturesRenderPass>();
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPasses.TryGetValue(renderingData.cameraData.camera, out var renderPass))
            {
                if (renderPass.CommandBuffers.Count > 0)
                    renderer.EnqueuePass(renderPass);
            }
        }

        public void AddCommandBuffer(Camera camera, CommandBuffer commandBuffer)
        {
            if (!_renderPasses.TryGetValue(camera, out var renderPass))
            {
                renderPass = new UpdateImpostorsTexturesRenderPass(() => _clearBufferAfterPass);
                // Configures where the render pass should be injected.
                renderPass.renderPassEvent = RenderPassEvent.BeforeRendering;
                _renderPasses.Add(camera, renderPass);
            }

            renderPass.CommandBuffers.Add(commandBuffer);
        }

        public void Clear(Camera mainCamera)
        {
            if (_renderPasses.TryGetValue(mainCamera, out var renderPass))
                renderPass.CommandBuffers.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (_renderPasses != null)
            {
                foreach (var renderPass in _renderPasses.Values)
                {
                    renderPass?.CommandBuffers?.Clear();
                    renderPass?.Dispose();
                }
            }

            _renderPasses?.Clear();
            _renderPasses = null;
        }


#if UNITY_2021_2_OR_NEWER
        class UpdateImpostorsTexturesRenderPass : ScriptableRenderPass
        {
            private readonly Func<bool> _clearBufferAfterPass;
            public readonly List<CommandBuffer> CommandBuffers;
            private RTHandle _dummyRTHandle;

            public UpdateImpostorsTexturesRenderPass(Func<bool> clearBufferAfterPass)
            {
                _clearBufferAfterPass = clearBufferAfterPass;
                CommandBuffers = new List<CommandBuffer>();
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (_dummyRTHandle == null || _dummyRTHandle.rt == null)
                {
                    _dummyRTHandle?.Release();

                    var rtFormat = RenderTextureFormat.R8;
                    if (RenderingUtils.SupportsRenderTextureFormat(rtFormat) == false)
                        rtFormat = RenderTextureFormat.Default;
                    GraphicsFormat graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(rtFormat, false);
                    graphicsFormat = SystemInfo.GetCompatibleFormat(graphicsFormat, FormatUsage.Render);
                    
                    _dummyRTHandle = RTHandles.Alloc(2, 2, (int)TextureWrapMode.Clamp, (DepthBits)TextureWrapMode.Clamp,
                        colorFormat: graphicsFormat);
                }
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // This dummy render target is used to fix any implicit problems related to changing render targets in URP.
                // Originally, this problem was on Oculus Quest device.
                ConfigureTarget(_dummyRTHandle);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                foreach (var commandBuffer in CommandBuffers)
                {
                    context.ExecuteCommandBuffer(commandBuffer);
                }
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (_clearBufferAfterPass.Invoke())
                    CommandBuffers.Clear();
            }

            internal void Dispose()
            {
                _dummyRTHandle?.Release();
                _dummyRTHandle = null;
            }
        }
#else
        class UpdateImpostorsTexturesRenderPass : ScriptableRenderPass
        {
            private readonly Func<bool> _clearBufferAfterPass;
            public readonly List<CommandBuffer> CommandBuffers;
            private RenderTexture _dummyRenderTarget;

            public UpdateImpostorsTexturesRenderPass(Func<bool> clearBufferAfterPass)
            {
                _clearBufferAfterPass = clearBufferAfterPass;
                CommandBuffers = new List<CommandBuffer>();
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in an performance manner.
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // this dummy render target is used to fix any implicit problems related to changing render targets in URP.
                _dummyRenderTarget = RenderTexture.GetTemporary(2, 2);
                ConfigureTarget(_dummyRenderTarget);
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                foreach (var commandBuffer in CommandBuffers)
                {
                    context.ExecuteCommandBuffer(commandBuffer);
                }
            }

            /// Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (_clearBufferAfterPass.Invoke())
                    CommandBuffers.Clear();
                RenderTexture.ReleaseTemporary(_dummyRenderTarget);
            }

            internal void Dispose()
            {
                if (_dummyRenderTarget != null)
                    RenderTexture.ReleaseTemporary(_dummyRenderTarget);
            }
        }
#endif
    }
}
#endif