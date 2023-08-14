using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct CalculateVisibleObjectsCountJob : IJob
    {
        public NativeArray<InstanceData> impostors;

        public void Execute()
        {
            int visibleCount = 0;
            int needUpdateCount = 0;
            for (int i = 0; i < impostors.Length; i++)
            {
                if (impostors[i].visibleState != 0)
                    visibleCount++;
                if (impostors[i].requiredAction == InstanceData.RequiredAction.UpdateImpostorTexture)
                    needUpdateCount++;
            }

            //Debug.Log($"Visible count: {visibleCount}, Need update count: {needUpdateCount}");
        }
    }
}