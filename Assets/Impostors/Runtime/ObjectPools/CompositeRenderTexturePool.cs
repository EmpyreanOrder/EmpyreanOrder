using System;
using System.Collections.Generic;
using UnityEngine;

namespace Impostors.ObjectPools
{
    [Serializable]
    public class CompositeRenderTexturePool
    {
        [SerializeField]
        private List<RenderTextureObjectPool> _pools;

        public CompositeRenderTexturePool(int[] supportedSizes, int initialCapacity, int depth, bool useMipMap,
            float mipMapBias, RenderTextureFormat renderTextureFormat)
        {
            _pools = new List<RenderTextureObjectPool>(supportedSizes.Length);
            foreach (var size in supportedSizes)
            {
                var pool = new RenderTextureObjectPool(initialCapacity, size, depth, useMipMap, mipMapBias,
                    renderTextureFormat);
                _pools.Add(pool);
            }
        }

        public RenderTexture Get(int textureSize)
        {
            foreach (var pool in _pools)
            {
                if (pool.TextureSize == textureSize)
                {
                    return pool.Get();
                }
            }

            throw new ArgumentOutOfRangeException(nameof(textureSize),
                $"This CompositeRenderTexturePool does not support specified texture size: {textureSize}.");
        }

        public void Return(RenderTexture renderTexture)
        {
            var textureSize = renderTexture.width;
            foreach (var pool in _pools)
            {
                if (pool.TextureSize == textureSize)
                {
                    pool.Return(renderTexture);
                    return;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(textureSize),
                $"This CompositeRenderTexturePool does not support specified texture size: {textureSize}.");
        }
    }
}