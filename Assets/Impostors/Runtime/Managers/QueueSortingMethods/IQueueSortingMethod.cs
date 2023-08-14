using Impostors.Structs;
using Unity.Collections;
using Unity.Jobs;

namespace Impostors.Managers.QueueSortingMethods
{
    public interface IQueueSortingMethod
    {
        /// <summary>
        /// In your job '<paramref name="instanceDataArray"/>' array must be with [<see cref="ReadOnlyAttribute"/>] attribute
        /// </summary>
        /// <param name="instanceDataArray"></param>
        /// <param name="updateQueue"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        JobHandle Sort(NativeArray<InstanceData> instanceDataArray,
            NativeQueue<int> updateQueue,
            JobHandle dependsOn);
    }
}