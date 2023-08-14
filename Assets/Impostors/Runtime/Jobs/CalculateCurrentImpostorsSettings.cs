using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct CalculateCurrentImpostorsSettings : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<SharedData> sharedDataArray;
        public NativeArray<InstanceData> instances;
        public float3 cameraPosition;
        public float multiplier;

        public void Execute(int index)
        {
            InstanceData instanceData = instances[index];
            instanceData.nowDirection = sharedDataArray[index].data.position - cameraPosition;
            instanceData.nowDistance = math.length(instanceData.nowDirection);
            instanceData.nowScreenSize = sharedDataArray[index].data.quadSize / (instanceData.nowDistance * multiplier);
            instances[index] = instanceData;
        }
    }
}