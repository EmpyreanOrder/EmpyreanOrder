using System;
using UnityEngine;

namespace Impostors.ObjectPools
{
    [Serializable]
    public class RenderTextureObjectPool : ObjectPool<RenderTexture>
    {
        public int TextureSize;
        public int Depth { get; }
        public bool UseMipMap { get; }
        public float MipMapBias { get; }
        public RenderTextureFormat RenderTextureFormat { get; }

        public RenderTextureObjectPool(int initialCapacity, int textureSize, int depth, bool useMipMap,
            float mipMapBias, RenderTextureFormat renderTextureFormat) : base(initialCapacity)
        {
            TextureSize = textureSize;
            Depth = depth;
            UseMipMap = useMipMap;
            MipMapBias = mipMapBias;
            RenderTextureFormat = renderTextureFormat;
        }

        public override RenderTexture Get()
        {
            var rt = base.Get();
            rt.Create();
            return rt;
        }

        protected override RenderTexture CreateObjectInstance()
        {
            var result = new RenderTexture(TextureSize, TextureSize, Depth, RenderTextureFormat);
            result.useMipMap = UseMipMap;
            result.autoGenerateMips = false;
            result.mipMapBias = MipMapBias;
            result.vrUsage = VRTextureUsage.None;
            result.Create();
            return result;
        }

        protected override void ProcessReturnedInstance(RenderTexture instance)
        {
            instance.DiscardContents();
            instance.Release();
        }
    }
}