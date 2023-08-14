using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct AddImpostorToUpdateQueueJob : IJob
    {
        public NativeQueue<int> queue;
        [ReadOnly]
        public NativeArray<InstanceData> impostors;

        public void Execute()
        {
            queue.Clear();
            for (int i = 0; i < impostors.Length; i++)
            {
                if (impostors[i].requiredAction > 0)
                {
                    queue.Enqueue(i);
                }
            }
        }
    }
}