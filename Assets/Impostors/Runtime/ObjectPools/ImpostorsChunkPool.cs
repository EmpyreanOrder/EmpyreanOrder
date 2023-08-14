using System;
using System.Collections.Generic;
using Impostors.ObjectPools;
using Impostors.TimeProvider;
using UnityEngine;

namespace Impostors
{
    [Serializable]
    public class ImpostorsChunkPool : IDisposable
    {
        private readonly int _atlasResolution;
        private readonly ITimeProvider _timeProvider;
        private readonly CompositeRenderTexturePool _renderTexturePool;
        private readonly MaterialObjectPool _materialObjectPool;
        private readonly Dictionary<int, ImpostorsChunk> _idToInstance;
        private readonly Dictionary<int, List<ImpostorsChunk>> _textureResolutionToChunkPool;
        [SerializeField]
        private List<ImpostorsChunk> _chunks;

        public IReadOnlyList<ImpostorsChunk> Chunks => _chunks;
        
        public ImpostorsChunkPool(int[] supportedSizes, int atlasResolution, ITimeProvider timeProvider,
            CompositeRenderTexturePool renderTexturePool, MaterialObjectPool materialObjectPool)
        {
            _atlasResolution = atlasResolution;
            _timeProvider = timeProvider;
            _renderTexturePool = renderTexturePool;
            _materialObjectPool = materialObjectPool;
            _idToInstance = new Dictionary<int, ImpostorsChunk>();
            _textureResolutionToChunkPool = new Dictionary<int, List<ImpostorsChunk>>(supportedSizes.Length);
            _chunks = new List<ImpostorsChunk>();
            for (int i = 0; i < supportedSizes.Length; i++)
            {
                int resolution = supportedSizes[i];
                _textureResolutionToChunkPool.Add(resolution, new List<ImpostorsChunk>());
            }
        }

        public ImpostorsChunk GetById(int id)
        {
            return _idToInstance[id];
        }

        public ImpostorsChunk GetWithPlace(int textureResolution)
        {
            var pool = _textureResolutionToChunkPool[textureResolution];
            if (pool.Count == 0)
            {
                return Create(textureResolution);
            }
            
            var chunk = pool[pool.Count - 1];
            
            if (chunk.EmptyPlacesCount > 0)
            {
                if (chunk.EmptyPlacesCount == 1)
                {
                    int index = -1;
                    for (int i = 0, count = pool.Count; i < count; i++)
                    {
                        if (pool[i].EmptyPlacesCount > 1)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index != -1)
                    {
                        var temp = pool[index];
                        pool[index] = pool[pool.Count - 1];
                        pool[pool.Count - 1] = temp;
                    }
                }

                return chunk;
            }

            return Create(textureResolution);
        }

        public void Return(ImpostorsChunk chunk)
        {
            if (!chunk.IsEmpty)
            {
                Debug.LogWarning($"Returning not empty ImpostorChunk '{chunk.Id}'");
            }

            _chunks.Remove(chunk);
            _idToInstance.Remove(chunk.Id);
            _textureResolutionToChunkPool[chunk.TextureResolution].Remove(chunk);
            chunk.Dispose();
        }

        private ImpostorsChunk Create(int textureResolution)
        {
            ImpostorsChunk chunk = new ImpostorsChunk(_atlasResolution, textureResolution,
                _timeProvider, _renderTexturePool, _materialObjectPool);
            _chunks.Add(chunk);
            _idToInstance.Add(chunk.Id, chunk);
            var pool = _textureResolutionToChunkPool[textureResolution];
            pool.Add(chunk);
            return chunk;
        }

        public void Dispose()
        {
            foreach (var chunk in _chunks)
            {
                chunk.Dispose();
            }

            _chunks.Clear();
            _idToInstance.Clear();
            _textureResolutionToChunkPool.Clear();
        }

        public void DestroyEmpty()
        {
            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                if (_chunks[i].IsEmpty)
                {
                    Return(_chunks[i]);
                }   
            }
        }
    }
}