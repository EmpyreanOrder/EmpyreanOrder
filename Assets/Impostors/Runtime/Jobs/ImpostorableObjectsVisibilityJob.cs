using System;
using Impostors.Structs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Impostors.Jobs
{
    [BurstCompile]
    public struct ImpostorableObjectsVisibilityJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<SimplePlane> cameraPlanes;

        [ReadOnly]
        public NativeArray<SharedData> sharedDataArray;

        public NativeArray<InstanceData> impostors;

        public void Execute(int index)
        {
            bool isVisible = true;
            InstanceData instanceData = impostors[index];
            float3 vmin, vmax;
            float3 objectPosition = sharedDataArray[index].data.position;
            float3 size = sharedDataArray[index].data.size;
            float3 boundsMin = objectPosition - size;
            float3 boundsMax = objectPosition + size;

            for (int i = 0; i < cameraPlanes.Length; i++)
            {
                float3 normal = cameraPlanes[i].normal;
                float distance = cameraPlanes[i].distance;

                // X axis
                if (normal.x < 0)
                {
                    vmin.x = boundsMin.x;
                    vmax.x = boundsMax.x;
                }
                else
                {
                    vmin.x = boundsMax.x;
                    vmax.x = boundsMin.x;
                }

                // Y axis
                if (normal.y < 0)
                {
                    vmin.y = boundsMin.y;
                    vmax.y = boundsMax.y;
                }
                else
                {
                    vmin.y = boundsMax.y;
                    vmax.y = boundsMin.y;
                }

                // Z axis
                if (normal.z < 0)
                {
                    vmin.z = boundsMin.z;
                    vmax.z = boundsMax.z;
                }
                else
                {
                    vmin.z = boundsMax.z;
                    vmax.z = boundsMin.z;
                }

                var dot1 = normal.x * vmin.x + normal.y * vmin.y + normal.z * vmin.z;
                if (dot1 + distance < 0)
                {
                    isVisible = false;
                    break;
                }
            }

            if (isVisible)
            {
                switch (instanceData.visibleState)
                {
                    case InstanceData.VisibilityState.NotSet:
                    case InstanceData.VisibilityState.BecameInvisible:
                    case InstanceData.VisibilityState.Invisible:
                        instanceData.visibleState = InstanceData.VisibilityState.BecameVisible;
                        break;
                    case InstanceData.VisibilityState.BecameVisible:
                    case InstanceData.VisibilityState.Visible:
                        instanceData.visibleState = InstanceData.VisibilityState.Visible;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                switch (instanceData.visibleState)
                {
                    case InstanceData.VisibilityState.NotSet:
                    case InstanceData.VisibilityState.BecameVisible:
                    case InstanceData.VisibilityState.Visible:
                        instanceData.visibleState = InstanceData.VisibilityState.BecameInvisible;
                        break;
                    case InstanceData.VisibilityState.BecameInvisible:
                    case InstanceData.VisibilityState.Invisible:
                        instanceData.visibleState = InstanceData.VisibilityState.Invisible;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            impostors[index] = instanceData;
        }
    }
}