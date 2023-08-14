using Unity.Mathematics;

namespace Impostors.Structs
{
    [System.Serializable]
    public struct SharedData
    {
        public int impostorLODGroupInstanceId;
        public int indexInManagers;

        [System.Serializable]
        public struct ImpostorData
        {
            public bool isPositionChanged;
            public float3 position;
            public float3 forward;
            public float3 lossyScale;
            public float3 size;
            public float height;
            public float quadSize;
            public float zOffset;
            // data to build runtime data
            public float3 localReferencePoint;
        }

        public ImpostorData data;

        [System.Serializable]
        public struct ImpostorSettings
        {
            public bool isStatic;
            public float fadeInTime;
            public float fadeOutTime;
            public float fadeTransitionTime;
            public float deltaCameraAngle;
            public byte useUpdateByTime;
            public float timeInterval;
            public byte useDeltaLightAngle;
            public float deltaLightAngle;
            public float deltaDistance;
            public int minTextureResolution;
            public int maxTextureResolution;
            public float screenRelativeTransitionHeight;
            public float screenRelativeTransitionHeightCull;
        }

        public ImpostorSettings settings;
    }
}