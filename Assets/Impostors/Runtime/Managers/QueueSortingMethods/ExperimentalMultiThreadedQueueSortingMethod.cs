using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Impostors.Managers.QueueSortingMethods
{
    [AddComponentMenu("")]
    public class ExperimentalMultiThreadedQueueSortingMethod : QueueSortingMethodBase
    {
        [Range(1, 100)]
        public int maxVisibleObjectsUpdatesPerFrame = 20;

        [Range(1, 50)]
        public int maxInvisibleObjectsUpdatesPerFrame = 10;

        [Range(0.1f, 50)]
        public float screenSizeWeight = 50f;

        [Range(0.01f, 10)]
        public float errorAngleWeight = 1f;

        [Range(0f, 1f)]
        [SerializeField]
        private float _randomness = .2f;
        
        public override JobHandle Sort(NativeArray<InstanceData> instanceDataArray, NativeQueue<int> updateQueue, JobHandle dependsOn)
        {
            
            var random = new Unity.Mathematics.Random((uint) Random.value * 100000 + 1);
            float min = 1;
            float max = 1 + _randomness;
            var importanceArray = new NativeArray<float>(instanceDataArray.Length, Allocator.TempJob);
            var visibleBuffer = new NativeList<IndexValuePair>(maxVisibleObjectsUpdatesPerFrame, Allocator.TempJob);
            var invisibleBuffer = new NativeList<IndexValuePair>(maxInvisibleObjectsUpdatesPerFrame, Allocator.TempJob);
            var updateCounter = new NativeArray<int>(1, Allocator.TempJob);

            var calculateUpdateImportanceJob = new CalculateUpdateImportance()
            {
                instanceDataArray = instanceDataArray,
                importanceArray = importanceArray,
                time = ImpostorLODGroupsManager.Instance.TimeProvider.Time,
                screenSizeWeight = screenSizeWeight,
                timeSinceLastUpdateWeight = errorAngleWeight,
                random = random,
                minRandomness = min,
                maxRandomness = max,
            };

            var findImmediateUpdateJob = new FindImpostorsThatRequireImmediateUpdateJob()
            {
                instanceDataArray = instanceDataArray,
                queue = updateQueue,
                maxImmediateUpdates = 5000,
                updateCount = updateCounter
            };

            var findVisibleJob = new FindKMaxImportanceJob()
            {
                buffer = visibleBuffer,
                visibility = 1,
                importanceArray = importanceArray,
                instanceDataArray = instanceDataArray,
                maxUpdateCount = maxVisibleObjectsUpdatesPerFrame,
                updateCounter = updateCounter
            };

            var findInvisibleJob = new FindKMaxImportanceJob()
            {
                buffer = invisibleBuffer,
                visibility = 0,
                importanceArray = importanceArray,
                instanceDataArray = instanceDataArray,
                maxUpdateCount = maxInvisibleObjectsUpdatesPerFrame,
                updateCounter = updateCounter
            };

            var fillUpdateQueueJob = new FillUpdateQueueJob()
            {
                visibleImpostorsBuffer = visibleBuffer,
                invisibleImpostorsBuffer = invisibleBuffer,
                updateQueue = updateQueue
            };

            var importanceJobHandle = calculateUpdateImportanceJob.Schedule(instanceDataArray.Length, dependsOn);
            var findImmediateUpdateJobHandle = findImmediateUpdateJob.Schedule(dependsOn);
            var jobHandle = JobHandle.CombineDependencies(importanceJobHandle, findImmediateUpdateJobHandle);
            var findVisibleJobHandle = findVisibleJob.Schedule(jobHandle);
            var findInvisibleJobHandle = findInvisibleJob.Schedule(jobHandle);
            var fillBuffersJobHandle = JobHandle.CombineDependencies(findVisibleJobHandle, findInvisibleJobHandle);
            var fillUpdateQueueJobHandle = fillUpdateQueueJob.Schedule(fillBuffersJobHandle);

            importanceArray.Dispose(fillBuffersJobHandle);
            visibleBuffer.Dispose(fillUpdateQueueJobHandle);
            invisibleBuffer.Dispose(fillUpdateQueueJobHandle);
            updateCounter.Dispose(fillUpdateQueueJobHandle);

            return fillUpdateQueueJobHandle;
        }
        
        [BurstCompile]
        private struct FillUpdateQueueJob : IJob
        {
            // input
            public NativeList<IndexValuePair> visibleImpostorsBuffer;
            public NativeList<IndexValuePair> invisibleImpostorsBuffer;

            // output
            public NativeQueue<int> updateQueue;

            public void Execute()
            {
                for (int i = 0; i < visibleImpostorsBuffer.Length; i++)
                {
                    updateQueue.Enqueue(visibleImpostorsBuffer[i].index);
                }

                for (int i = 0; i < invisibleImpostorsBuffer.Length; i++)
                {
                    updateQueue.Enqueue(invisibleImpostorsBuffer[i].index);
                }
            }
        }

        [BurstCompile]
        private struct FindKMaxImportanceJob : IJob
        {
            // input
            [ReadOnly]
            public NativeArray<InstanceData> instanceDataArray;

            [ReadOnly]
            public NativeArray<float> importanceArray;

            [ReadOnly]
            public NativeArray<int> updateCounter;

            public int maxUpdateCount;

            public int visibility;

            // output
            public NativeList<IndexValuePair> buffer;

            public void Execute()
            {
                int length = importanceArray.Length;
                int maxCount = maxUpdateCount - updateCounter[0];
                if (maxCount <= 0)
                    return;

                int smallestBuffIndex = -1;
                float smallestBuffValue = float.MinValue;

                for (int i = 0; i < length; i++)
                {
                    var importance = importanceArray[i];

                    if (importance < 0) // this means that impostor is already in update queue and we need to skip it
                        continue;
                    var instanceData = instanceDataArray[i];

                    if (instanceData.visibleState == InstanceData.VisibilityState.NotSet)
                        continue;

                    if (instanceData.requiredAction != InstanceData.RequiredAction.UpdateImpostorTexture)
                        continue;

                    if (visibility == 0 && (instanceData.visibleState == InstanceData.VisibilityState.Visible ||
                                            instanceData.visibleState == InstanceData.VisibilityState.BecameVisible))
                        continue;

                    if (visibility == 1 && (instanceData.visibleState == InstanceData.VisibilityState.Invisible ||
                                            instanceData.visibleState == InstanceData.VisibilityState.BecameInvisible))
                        continue;

                    // fill buffer with any value until it reaches maxCount
                    if (buffer.Length < maxCount)
                    {
                        buffer.Add(new IndexValuePair(i, importance));
                    }
                    // when buffer length == maxCount
                    else
                    {
                        // if there is no smallest importance
                        if (smallestBuffIndex == -1)
                        {
                            // then find new smallest importance in buffer
                            smallestBuffIndex = 0;
                            smallestBuffValue = float.MaxValue;
                            for (int j = 0; j < maxCount; j++)
                            {
                                if (buffer[j].value < smallestBuffValue)
                                {
                                    smallestBuffValue = buffer[j].value;
                                    smallestBuffIndex = j;
                                }
                            }
                        }

                        // if smallest importance in buffer less than current importance 
                        if (importance > smallestBuffValue)
                        {
                            // then replace smallest importance with current importance
                            buffer[smallestBuffIndex] = new IndexValuePair(i, importance);
                            smallestBuffIndex = -1;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct FindImpostorsThatRequireImmediateUpdateJob : IJob
        {
            [ReadOnly]
            public NativeArray<InstanceData> instanceDataArray;

            [WriteOnly]
            public NativeQueue<int> queue;

            [WriteOnly]
            public NativeArray<int> updateCount;

            public float maxImmediateUpdates;

            public void Execute()
            {
                int updateCounter = 0;
                int length = instanceDataArray.Length;
                // adding immediate actions into queue buffer
                for (int i = 0; i < length; i++)
                {
                    var reqAction = instanceDataArray[i].requiredAction;
                    if (reqAction == InstanceData.RequiredAction.Cull ||
                        reqAction == InstanceData.RequiredAction.GoToImpostorMode ||
                        reqAction == InstanceData.RequiredAction.GoToNormalMode)
                    {
                        queue.Enqueue(i);
                        if (reqAction == InstanceData.RequiredAction.GoToImpostorMode)
                            updateCounter++;

                        if (updateCounter > maxImmediateUpdates)
                            break;
                    }
                }

                updateCount[0] = updateCounter;
            }
        }

        [BurstCompile]
        private struct CalculateUpdateImportance : IJobFor
        {
            [ReadOnly]
            public NativeArray<InstanceData> instanceDataArray;

            [WriteOnly]
            public NativeArray<float> importanceArray;

            public float time;
            public float screenSizeWeight;
            public float timeSinceLastUpdateWeight;
            public Unity.Mathematics.Random random;
            public float minRandomness;
            public float maxRandomness;

            public void Execute(int index)
            {
                var instanceData = instanceDataArray[index];
                float value = instanceData.nowScreenSize * screenSizeWeight +
                              instanceData.angleDifferenceSinceLastUpdate * timeSinceLastUpdateWeight;
                value *= random.NextFloat(minRandomness, maxRandomness);
                value *= instanceData.visibleState == InstanceData.VisibilityState.BecameVisible ? 100 : 1;

                var reqAction = instanceDataArray[index].requiredAction;
                value = reqAction == InstanceData.RequiredAction.Cull ||
                        reqAction == InstanceData.RequiredAction.GoToImpostorMode ||
                        reqAction == InstanceData.RequiredAction.GoToNormalMode
                    ? -1
                    : value;

                importanceArray[index] = value;
            }
        }

        private struct IndexValuePair
        {
            public int index;
            public float value;

            public IndexValuePair(int index, float value)
            {
                this.index = index;
                this.value = value;
            }
        }
    }
}