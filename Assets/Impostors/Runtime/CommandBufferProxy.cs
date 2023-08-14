using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Impostors
{
    public sealed class CommandBufferProxy
    {
        private readonly List<Keyword> _keywords;
        private readonly List<RendererMaterialPropertyBlockPair> _resetRenderersList;
        private readonly Queue<MaterialPropertyBlock> _materialPropertyBlocksPool;
        private readonly MaterialPropertyBlock _emptyPropertyBlock;
        private bool _invertCulling;

        public CommandBufferProxy()
        {
            CommandBuffer = new CommandBuffer();
            _keywords = new List<Keyword>(5)
            {
                new Keyword(nameof(ShaderKeywords.LIGHTMAP_ON)),
                new Keyword(nameof(ShaderKeywords.LIGHTPROBE_SH)),
                new Keyword(nameof(ShaderKeywords.SHADOWS_SHADOWMASK)),
                new Keyword(nameof(ShaderKeywords.DIRLIGHTMAP_COMBINED)),
                new Keyword(nameof(ShaderKeywords.SHADOWS_SCREEN))
            };
            _resetRenderersList = new List<RendererMaterialPropertyBlockPair>();
            _materialPropertyBlocksPool = new Queue<MaterialPropertyBlock>();
            _emptyPropertyBlock = new MaterialPropertyBlock();
            
            Clear();
        }

        public CommandBuffer CommandBuffer { get; }

        public void EnableShaderKeyword(int id)
        {
            var keyword = _keywords[id];
            if (keyword.State != KeywordState.Enabled)
            {
                CommandBuffer.EnableShaderKeyword(keyword.Name);
                keyword.State = KeywordState.Enabled;
            }
        }

        public void DisableShaderKeyword(int id)
        {
            var keyword = _keywords[id];
            if (keyword.State != KeywordState.Disabled)
            {
                CommandBuffer.DisableShaderKeyword(keyword.Name);
                keyword.State = KeywordState.Disabled;
            }
        }

        public int GetOrRegisterKeywordId(string keywordName)
        {
            for (int i = 0; i < _keywords.Count; i++)
            {
                if (_keywords[i].Name == keywordName)
                    return i;
            }

            _keywords.Add(new Keyword(keywordName));
            return _keywords.Count - 1;
        }

        public void SetInvertCulling(bool value)
        {
            if (_invertCulling != value)
            {
                _invertCulling = value;
                CommandBuffer.SetInvertCulling(value);
            }
        }

        public void DrawRenderer(Renderer renderer, Material material, int submeshIndex, int shaderPass,
            MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock != null && propertyBlock.isEmpty == false)
            {
                var originalPropertyBlock = GetMaterialPropertyBlock();
                renderer.GetPropertyBlock(originalPropertyBlock);
                if (originalPropertyBlock.isEmpty)
                {
                    ReturnMaterialPropertyBlock(originalPropertyBlock);
                    originalPropertyBlock = _emptyPropertyBlock;
                }
                _resetRenderersList.Add(new RendererMaterialPropertyBlockPair(renderer, originalPropertyBlock));
                renderer.SetPropertyBlock(propertyBlock);
            }
            
            CommandBuffer.DrawRenderer(renderer, material, submeshIndex, shaderPass);
        }

        public void ResetRenderers()
        {
            foreach (var pair in _resetRenderersList)
            {
                pair.Renderer.SetPropertyBlock(pair.MaterialPropertyBlock);
                ReturnMaterialPropertyBlock(pair.MaterialPropertyBlock);
            }
            _resetRenderersList.Clear();
        }
        
        public void Clear()
        {
            CommandBuffer.Clear();
            for (int i = 0; i < _keywords.Count; i++)
            {
                _keywords[i].State = KeywordState.Unknown;
            }

            _invertCulling = false;
            CommandBuffer.SetInvertCulling(false);
            ResetRenderers();
        }

        public void Dispose()
        {
            CommandBuffer.Dispose();
            _keywords.Clear();
            _emptyPropertyBlock.Clear();
            _resetRenderersList.Clear();
            _materialPropertyBlocksPool.Clear();
        }

        private MaterialPropertyBlock GetMaterialPropertyBlock()
        {
            if (_materialPropertyBlocksPool.Count > 0)
                return _materialPropertyBlocksPool.Dequeue();

            return new MaterialPropertyBlock();
        }

        private void ReturnMaterialPropertyBlock(MaterialPropertyBlock block)
        {
            if (block == _emptyPropertyBlock)
                return;
            _materialPropertyBlocksPool.Enqueue(block);
        }

        private class Keyword
        {
            public readonly string Name;
            public KeywordState State;

            public Keyword(string name)
            {
                Name = name;
                State = KeywordState.Unknown;
            }
        }
        
        private enum KeywordState
        {
            Unknown,
            Disabled,
            Enabled
        }

        private struct RendererMaterialPropertyBlockPair
        {
            public Renderer Renderer;
            public MaterialPropertyBlock MaterialPropertyBlock;

            public RendererMaterialPropertyBlockPair(Renderer renderer, MaterialPropertyBlock materialPropertyBlock)
            {
                Renderer = renderer;
                MaterialPropertyBlock = materialPropertyBlock;
            }
        }
    }
}