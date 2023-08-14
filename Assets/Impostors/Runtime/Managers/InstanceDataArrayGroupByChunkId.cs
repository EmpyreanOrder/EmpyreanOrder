using System.Collections.Generic;
using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Impostors.Managers
{
    public static class InstanceDataArrayGroupByChunkId
    {
        public static void Group(NativeArray<InstanceData> instanceDataArray, List<int> indexes)
        {
            var nativeArray = indexes.ToNativeArray(Allocator.TempJob);
            var job = new SortJob()
            {
                instanceDataArray = instanceDataArray,
                indexes = nativeArray
            };
            job.Run();
            for (int i = 0; i < nativeArray.Length; i++)
            {
                indexes[i] = nativeArray[i];
            }
            nativeArray.Dispose();
        }

        [BurstCompile]
        private struct SortJob : IJob
        {
            [ReadOnly]
            public NativeArray<InstanceData> instanceDataArray;

            public NativeArray<int> indexes;

            public void Execute()
            {
                for (int i = 0; i < indexes.Length; i++)
                {
                    var indexI = indexes[i];
                    var chunkId = instanceDataArray[indexI].ChunkId;
                    for (int j = i + 1; j < indexes.Length; j++)
                    {
                        var indexJ = indexes[j];
                        if (instanceDataArray[indexJ].ChunkId == chunkId)
                        {
                            i++;
                            if (j != i)
                            {
                                var temp = indexes[i];
                                indexes[i] = indexes[j];
                                indexes[j] = temp;
                            }
                        }
                    }
                }
            }
        }
    }
}