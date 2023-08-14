using Unity.Mathematics;

namespace Impostors.Structs
{
    [System.Serializable]
    public struct Impostor
    {
        public bool Exists => impostorLODGroupInstanceId != 0;

        public int impostorLODGroupInstanceId;
        public int indexInManagers;
        public bool isRelevant;

        public float quadSize;
        public float zOffset;
        public float fadeTime;
        public float time;
        
        public float3 position;
        public float3 direction;
        public float4 uv;
    }
}