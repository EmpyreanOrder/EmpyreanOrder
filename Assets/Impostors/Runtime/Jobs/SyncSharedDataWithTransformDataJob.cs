using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct SyncSharedDataWithTransformDataJob : IJobParallelForTransform
    {
        public bool forceUpdateForStatic;
        public float positionChangeDeltaSquared;
        public NativeArray<SharedData> sharedDataArray;

        public void Execute(int index, [ReadOnly] TransformAccess transform)
        {
            var sharedData = sharedDataArray[index];

            if (forceUpdateForStatic == false && sharedData.settings.isStatic)
            {
                if (sharedData.data.isPositionChanged)
                {
                    sharedData.data.isPositionChanged = false;
                    sharedDataArray[index] = sharedData;
                }

                return;
            }

            float4x4 localToWorldMatrix = transform.localToWorldMatrix;
            var newPosition = math.mul(localToWorldMatrix, new float4(sharedData.data.localReferencePoint, 1)).xyz;
            
            sharedData.data.isPositionChanged = false;
            if (math.lengthsq(newPosition - sharedData.data.position) > positionChangeDeltaSquared)
            {
                sharedData.data.isPositionChanged = true;
                sharedData.data.position = newPosition;
            }

            sharedData.data.forward = math.mul(transform.rotation, new float3(0, 0, 1));
            //sharedData.data.lossyScale = transform.localToWorldMatrix.lossyScale; // todo: looks like this operation is expensive, need to check UPD: consumes half of the time

            // var scale = new float3(
            //     math.length(localToWorldMatrix.c0), 
            //     math.length(localToWorldMatrix.c1),
            //     math.length(localToWorldMatrix.c2)); // this version uses 0.1 on 4k objects

            sharedDataArray[index] = sharedData;
        }
    }
}