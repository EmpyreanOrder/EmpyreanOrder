using UnityEngine;

namespace Impostors.RenderInstructions
{
    public sealed class DrawRendererInstruction : IRenderInstruction
    {
        public readonly Renderer Renderer;
        public readonly GameObject GameObject;
        public readonly Material Material;
        public readonly int SubmeshIndex;
        public readonly int ShaderPass;
        public readonly MaterialPropertyBlock PropertyBlock;

        public DrawRendererInstruction(Renderer renderer, Material material, int submeshIndex, int shaderPass,
            MaterialPropertyBlock propertyBlock)
        {
            Renderer = renderer;
            GameObject = renderer.gameObject;
            Material = material;
            SubmeshIndex = submeshIndex;
            ShaderPass = shaderPass;
            PropertyBlock = propertyBlock;
        }

        public void ApplyCommandBuffer(CommandBufferProxy bufferProxy)
        {
            if (GameObject.activeInHierarchy == false)
                return;
            bufferProxy.DrawRenderer(Renderer, Material, SubmeshIndex, ShaderPass, PropertyBlock);
        }
    }
}