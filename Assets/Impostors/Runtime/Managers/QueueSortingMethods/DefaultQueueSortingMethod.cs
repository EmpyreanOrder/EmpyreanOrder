using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Impostors.Managers.QueueSortingMethods
{
    public class DefaultQueueSortingMethod : QueueSortingMethodBase
    {
        [Tooltip("Controls how many imposters should be updated per frame.")]
        [Range(1, 100)]
        public int maxUpdatesPerFrame = 20;

        [Tooltip("By “invisible” it means objects that are not in camera’s frustum(outside the screen, behind the camera). " +
                 "No invisible objects updates are performed when regular updates exceed Max Updates Per Frame.")]
        [Range(1, 50)]
        public int maxInvisibleObjectsUpdatesPerFrame = 20;

        [Tooltip("Controls how imposter’s size on screen affects update priority.")]
        [Range(0.1f, 50)]
        public float screenSizeWeight = 50f;

        [Tooltip("Controls how imposter’s visual error angle affects update priority.")]
        [Range(0.01f, 10)]
        public float errorAngleWeight = 1f;

        [Tooltip("Adds a bit of randomness into sorting to prevent noticable update pattern over large areas.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _randomness = .2f;

        public override JobHandle Sort(NativeArray<InstanceData> instanceDataArray, NativeQueue<int> updateQueue,
            JobHandle dependsOn)
        {
            NativeList<IndexValuePair> buffer = new NativeList<IndexValuePair>(maxUpdatesPerFrame, Allocator.TempJob);
            var random = new Unity.Mathematics.Random((uint) Random.value + 1);
            float min = 1;
            float max = 1 + _randomness;
            var job = new Job()
            {
                instanceDataArray = instanceDataArray,
                buffer = buffer,
                queue = updateQueue,
                maxUpdates = maxUpdatesPerFrame,
                maxBackgroundUpdates = maxInvisibleObjectsUpdatesPerFrame,
                random = random,
                minRandomness = min,
                maxRandomness = max,
                screenSizeWeight = screenSizeWeight,
                errorAngleWeight = errorAngleWeight
            };
            dependsOn = job.Schedule(dependsOn);
            buffer.Dispose(dependsOn);
            return dependsOn;
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

        [BurstCompile]
        private struct Job : IJob
        {
            [ReadOnly]
            public NativeArray<InstanceData> instanceDataArray;

            public NativeList<IndexValuePair> buffer;
            public NativeQueue<int> queue;
            public int maxUpdates;
            public int maxBackgroundUpdates;
            public float screenSizeWeight;
            public float errorAngleWeight;
            public Unity.Mathematics.Random random;
            public float minRandomness;
            public float maxRandomness;

            public void Execute()
            {
                int updateCounter = 0;
                // adding immediate actions into queue buffer
                for (int i = 0; i < instanceDataArray.Length; i++)
                {
                    var reqAction = instanceDataArray[i].requiredAction;
                    if (reqAction == InstanceData.RequiredAction.Cull ||
                        reqAction == InstanceData.RequiredAction.GoToNormalMode)
                    {
                        buffer.Add(new IndexValuePair(i, float.MaxValue));
                        continue;
                    }

                    if (reqAction == InstanceData.RequiredAction.GoToImpostorMode && updateCounter < 5000)
                    {
                        updateCounter++;
                        buffer.Add(new IndexValuePair(i, float.MaxValue));
                    }
                }

                var bufferLengthBeforeDo = buffer.Length;
                // if buffer has place then add visible objects into queue
                if (updateCounter < maxUpdates)
                {
                    Do(1, maxUpdates - updateCounter, buffer.Length);
                }

                updateCounter += buffer.Length - bufferLengthBeforeDo;
                // if buffer has place then add invisible objects into queue
                if (updateCounter < maxUpdates)
                {
                    int backgroundUpdates = math.min(maxBackgroundUpdates, maxUpdates - updateCounter);
                    Do(0, backgroundUpdates, buffer.Length);
                }

                // filling queue with data from temp buffer
                for (int i = 0; i < buffer.Length; i++)
                {
                    queue.Enqueue(buffer[i].index);
                }
            }

            [BurstCompile]
            private void Do(int isVisible, int maxCount, int startBufferIndex)
            {
                int smallestBuffIndex = -1;
                float smallestBuffValue = float.MinValue;

                // "cool" sorting algorithm that respects max capacity of result 
                for (int i = 0; i < instanceDataArray.Length; i++)
                {
                    var instanceData = instanceDataArray[i];
                    if (instanceData.visibleState == InstanceData.VisibilityState.NotSet)
                        continue;
                    if (isVisible == 0 && (instanceData.visibleState == InstanceData.VisibilityState.Visible ||
                                           instanceData.visibleState == InstanceData.VisibilityState.BecameVisible))
                        continue;
                    if (isVisible == 1 && (instanceData.visibleState == InstanceData.VisibilityState.Invisible ||
                                           instanceData.visibleState == InstanceData.VisibilityState.BecameInvisible))
                        continue;
                    if (instanceData.requiredAction != InstanceData.RequiredAction.UpdateImpostorTexture)
                        continue;

                    float value = instanceData.nowScreenSize * screenSizeWeight +
                                  instanceDataArray[i].angleDifferenceSinceLastUpdate * errorAngleWeight;
                    value *= random.NextFloat(minRandomness, maxRandomness);
                    value *= instanceData.visibleState == InstanceData.VisibilityState.BecameVisible ? 100 : 1; 
                    if (buffer.Length - startBufferIndex < maxCount)
                    {
                        buffer.Add(new IndexValuePair(i, value));
                    }
                    else
                    {
                        // if there is no smallest value
                        if (smallestBuffIndex == -1)
                        {
                            // find new smallest value
                            smallestBuffIndex = startBufferIndex;
                            smallestBuffValue = float.MaxValue;
                            for (int j = startBufferIndex; j < buffer.Length; j++)
                            {
                                if (buffer[j].value < smallestBuffValue)
                                {
                                    smallestBuffValue = buffer[j].value;
                                    smallestBuffIndex = j;
                                }
                            }
                        }

                        if (value > smallestBuffValue)
                        {
                            // replace smallest value
                            buffer[smallestBuffIndex] = new IndexValuePair(i, value);
                            smallestBuffIndex = -1;
                        }
                    }
                }
            }
        }
    }
}