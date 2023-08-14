using UnityEngine;

namespace Impostors.RenderInstructions
{
    public sealed class RenderInstructionBufferBuilder
    {
        // 0 - unknown, 1 - disabled, 2 - enabled
        private readonly byte[] _keywords = new byte[4];
        private bool _invertCulling = false;
        private RenderInstructionBuffer _renderInstructionBuffer;

        public MaterialPropertyBlock PropertyBlock => _renderInstructionBuffer.PropertyBlock;

        public void Begin(int capacity)
        {
            for (int i = 0; i < _keywords.Length; i++)
                _keywords[i] = 0;
            _renderInstructionBuffer = new RenderInstructionBuffer(capacity);
            _invertCulling = false;
        }
        
        public void EnableShaderKeyword(int keywordId)
        {
            if (_keywords[keywordId] != 2)
            {
                _keywords[keywordId] = 2;
                _renderInstructionBuffer.Add(new EnableShaderKeywordInstruction(keywordId));
            }
        }

        public void DisableShaderKeyword(int keywordId)
        {
            if (_keywords[keywordId] != 1)
            {
                _keywords[keywordId] = 1;
                _renderInstructionBuffer.Add(new DisableShaderKeywordInstruction(keywordId));
            }
        }

        public void SetInvertCulling(bool value)
        {
            if (_invertCulling != value)
            {
                _invertCulling = value;
                _renderInstructionBuffer.Add(new SetInvertCulling(value));
            }
        }

        public void AddRenderInstruction(IRenderInstruction renderInstruction)
        {
            _renderInstructionBuffer.Add(renderInstruction);
        }
        
        public RenderInstructionBuffer Build()
        {
            var result = _renderInstructionBuffer;
            _renderInstructionBuffer = null;
            return result;
        }
    }
}