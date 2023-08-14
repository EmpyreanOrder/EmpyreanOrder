using Impostors.Structs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Impostors.Managers.QueueSortingMethods
{
    public abstract class QueueSortingMethodBase : MonoBehaviour, IQueueSortingMethod
    {
        public abstract JobHandle Sort(NativeArray<InstanceData> instanceDataArray,
            NativeQueue<int> updateQueue,
            JobHandle dependsOn);
    }
}