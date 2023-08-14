using System;
using Impostors.MemoryUsage;
using Unity.Jobs;
using UnityEngine;

namespace Impostors.ImpostorsChunkMesh
{
    public interface IImpostorsChunkMesh : IDisposable, IMemoryConsumer
    {
        JobHandle ScheduleMeshCreation(JobHandle jobHandle);
        Mesh GetMesh();
    }
}