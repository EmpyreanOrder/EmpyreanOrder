using Unity.Mathematics;

namespace Impostors.Structs
{
    [System.Serializable]
    public struct InstanceData
    {
        public int impostorLODGroupInstanceId;

        /// <summary>
        /// Is object currently visible
        /// </summary>
        public VisibilityState visibleState;

        /// <summary>
        /// Indicates if imposter has been created and ready to be shown
        /// </summary>
        public bool HasImpostor => lastUpdate.chunkId != 0;

        public enum VisibilityState : byte
        {
            NotSet = 0,
            BecameInvisible = 1,
            Invisible = 2,
            BecameVisible = 3,
            Visible = 4,
        }
        
        /// <summary>
        /// Does impostor need to update its texture
        /// '-1' not set.
        ///  '0' do not need update.
        ///  '1' need to update impostor
        ///  '2' need to go in impostor mode
        ///  '3' need to go in original mode
        /// </summary>
        public RequiredAction requiredAction;

        public enum RequiredAction
        {
            NotSet = -1,
            None = 0,
            UpdateImpostorTexture = 1,
            GoToImpostorMode = 2,
            GoToNormalMode = 3,
            Cull = 4,
            ForcedUpdateImpostorTexture = 5,
        }

        public float nowScreenSize;
        public float nowDistance;
        public float angleDifferenceSinceLastUpdate;
        public float3 nowDirection;

        public int ChunkId => lastUpdate.chunkId;
        public int PlaceInChunk => lastUpdate.placeInChunk;

        public void SetChunk(int chunkId, int placeInChunk)
        {
            lastUpdate.chunkId = chunkId;
            lastUpdate.placeInChunk = placeInChunk;
        }

        [System.Serializable]
        public struct LastUpdate
        {
            public int chunkId;
            public int placeInChunk;
            public float time;
            public float3 lightDirection;

            /// <summary>
            /// Direction form camera to impostor in last update.
            /// Equals to 'impostor.position - camera.position'
            /// </summary>
            public float3 cameraDirection;

            public float3 objectForwardDirection;
            public float screenSize;
            public float distance;
            public int textureResolution;
        }

        public LastUpdate lastUpdate;
    }
}