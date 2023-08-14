using UnityEngine;

namespace Impostors.RenderInstructions
{
    public sealed class SetInvertCulling : IRenderInstruction
    {
        private readonly bool _invert;

        public SetInvertCulling(bool invert)
        {
            _invert = invert;
        }
        public void ApplyCommandBuffer(CommandBufferProxy bufferProxy)
        {
            bufferProxy.SetInvertCulling(_invert);
        }

        public void ApplyMaterialPropertyBlock(MaterialPropertyBlock prop)
        {
        }
    }
}