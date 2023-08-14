using System;
using System.Collections.Generic;
using UnityEngine;

namespace Impostors.RenderInstructions
{
    public sealed class RenderInstructionBuffer
    {
        private readonly List<IRenderInstruction> _renderInstructions;
        public readonly MaterialPropertyBlock PropertyBlock;

        public RenderInstructionBuffer(int capacity)
        {
            _renderInstructions = new List<IRenderInstruction>(capacity);
            PropertyBlock = new MaterialPropertyBlock();
        }
        
        public void Add(IRenderInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));
            _renderInstructions.Add(instruction);
        }

        public void Apply(CommandBufferProxy bufferProxy)
        {
            for (int i = 0; i < _renderInstructions.Count; i++)
            {
                _renderInstructions[i].ApplyCommandBuffer(bufferProxy);
            }
        }
    }
}