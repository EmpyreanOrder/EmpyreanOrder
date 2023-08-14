using System;
using System.Runtime.InteropServices;
using Impostors.ImpostorsChunkMesh;
using Impostors.MemoryUsage;
using Impostors.ObjectPools;
using Impostors.Structs;
using Impostors.TimeProvider;
using Impostors.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Impostors
{
    [Serializable]
    public class ImpostorsChunk : IDisposable, IMemoryConsumer
    {
        private readonly CompositeRenderTexturePool _renderTexturePool;
        private readonly MaterialObjectPool _materialObjectPool;
        private readonly bool _useMipMap;

        [HideInInspector]
        [SerializeField]
        private string name;

        private NativeStack<int> _emptyPlaces;

        [SerializeField]
        private RenderTexture _renderTexture;

        [SerializeField]
        private Material _material;

        [SerializeField]
        private int _size;

        [SerializeField]
        private bool _needToRebuildMeshFlag;

        [SerializeField]
        private bool _needToClearTexture;

        private NativeArray<Impostor> _impostors;
        private NativeList<RemoveInfo> _removeInfoArray;
        private NativeArray<bool> _needToRebuildMeshNativeArray;

        [StructLayout(LayoutKind.Sequential)]
        private struct RemoveInfo
        {
            public int instanceId;
            // we need to track indexInManagers. When impostor is removing it swaps with the last impostor. 
            // That's why we swap indexes from-to.
            public int fromIndex;
            public int toIndex;
        }

        public int TextureResolution { get; }
        public int Id { get; }
        public int Capacity { get; }
        public int Count => Capacity - _emptyPlaces.Count;
        public bool IsFull => _emptyPlaces.Count == 0;
        public bool HasPlace => !IsFull;
        public bool IsEmpty => _emptyPlaces.Count == Capacity;
        public int EmptyPlacesCount => _emptyPlaces.Count;

        private static int ImpostorsChunkIdCounter;
        private ITimeProvider TimeProvider { get; }
        private IImpostorsChunkMesh ImpostorsChunkMesh { get; }

        internal bool NeedToRebuildMesh => _needToRebuildMeshFlag || _needToRebuildMeshNativeArray[0];

        private ImpostorsChunk()
        {
        }

        public ImpostorsChunk(int atlasResolution, int textureResolution, ITimeProvider timeProvider,
            CompositeRenderTexturePool renderTexturePool, MaterialObjectPool materialObjectPool)
        {
            _renderTexturePool = renderTexturePool;
            _materialObjectPool = materialObjectPool;
            TimeProvider = timeProvider;
            _size = atlasResolution / textureResolution;
            Capacity = _size * _size;

            Assert.AreEqual(math.ceilpow2(Capacity), Capacity);
            _impostors = new NativeArray<Impostor>(Capacity, Allocator.Persistent);
            _removeInfoArray = new NativeList<RemoveInfo>(Allocator.Persistent);
            _emptyPlaces = new NativeStack<int>(Capacity, Allocator.Persistent);
            _needToRebuildMeshNativeArray = new NativeArray<bool>(1, Allocator.Persistent);
            for (int i = 0; i < Capacity; i++)
            {
                _emptyPlaces.Push(i);
            }

            TextureResolution = textureResolution;
            Id = ++ImpostorsChunkIdCounter;

            _renderTexture = renderTexturePool.Get(atlasResolution);
            _useMipMap = _renderTexture.useMipMap;
            _needToClearTexture = true;
            _material = materialObjectPool.Get();
            _material.mainTexture = _renderTexture;
            // setting render queue to minimize overdraw effect
            _material.renderQueue = 2470 - InversePowerOfTwo(textureResolution);

            //ImpostorsChunkMesh = new DefaultImpostorsChunkMesh(_impostors, this);
            ImpostorsChunkMesh = new BufferedImpostorsChunkMesh(Capacity, _impostors);
            var mesh = ImpostorsChunkMesh.GetMesh();
            string tName = $"Chunk#{Id} {textureResolution}";
            mesh.name = tName;
            name = tName;
        }


        public int GetPlace(in InstanceData instanceData, in SharedData sharedData)
        {
            if (!HasPlace)
                throw new Exception("No place in chunk.");

            int index = _emptyPlaces.Pop();

            var impostor = _impostors[index];
            impostor.isRelevant = true;
            impostor.impostorLODGroupInstanceId = instanceData.impostorLODGroupInstanceId;
            impostor.indexInManagers = sharedData.indexInManagers;

            impostor.position = sharedData.data.position;
            impostor.direction = instanceData.nowDirection;
            impostor.quadSize = sharedData.data.quadSize;
            impostor.zOffset = sharedData.data.zOffset;

            var requiredAction = instanceData.requiredAction;
            switch (requiredAction)
            {
                case InstanceData.RequiredAction.UpdateImpostorTexture:
                    if (instanceData.visibleState == InstanceData.VisibilityState.BecameVisible)
                    {
                        impostor.fadeTime = 0; // object just appeared on screen so we can show it instantly
                    }
                    else
                        impostor.fadeTime = sharedData.settings.fadeTransitionTime;

                    break;
                case InstanceData.RequiredAction.GoToImpostorMode:
                    impostor.fadeTime = sharedData.settings.fadeInTime;
                    break;
                default:
                    impostor.fadeTime = sharedData.settings.fadeInTime;
                    Debug.LogError($"Unexpected required action: '{requiredAction.ToString()}'");
                    break;
            }

            impostor.uv = GetUV(index);
            impostor.time = Mathf.Max(TimeProvider.Time + impostor.fadeTime - TimeProvider.DeltaTime, 0);

            _impostors[index] = impostor;

            _needToRebuildMeshFlag = true;
            return index;
        }

        public void MarkPlaceAsNotRelevant(int place, float fadeTime, bool isLettingFadeInFirst)
        {
            var impostor = _impostors[place];
            impostor.isRelevant = false;
            if (isLettingFadeInFirst == false || impostor.time < TimeProvider.Time)
                impostor.time = -(TimeProvider.Time + fadeTime);
            impostor.fadeTime = fadeTime;
            _impostors[place] = impostor;
            _needToRebuildMeshFlag = true;
        }

        public JobHandle ScheduleUpdateImpostors(JobHandle jobHandle, NativeArray<SharedData> sharedDataArray)
        {
            int impostorsLength = _impostors.Length;
            if (_removeInfoArray.Length > 0)
            {
                var jobRemoveImpostors = new RemoveSpecifiedImpostors()
                {
                    impostors = _impostors,
                    emptyPlaces = _emptyPlaces,
                    removeInfoArray = _removeInfoArray
                };
                jobHandle = jobRemoveImpostors.Schedule(impostorsLength, jobHandle);

                var jobClearInstanceIdsList = new ClearInstanceIdsJob() {list = _removeInfoArray};
                jobHandle = jobClearInstanceIdsList.Schedule(jobHandle);
            }

            var jobUpdateImpostors = new UpdateImpostorsJob()
            {
                sharedDataArray = sharedDataArray,
                impostors = _impostors,
                emptyPlaces = _emptyPlaces,
                time = TimeProvider.Time,
                needRebuildMesh = _needToRebuildMeshNativeArray
            };
            jobHandle = jobUpdateImpostors.Schedule(impostorsLength, jobHandle);
            return jobHandle;
        }

        public JobHandle ScheduleMeshCreation(JobHandle dependsOn)
        {
            if (!NeedToRebuildMesh)
                throw new Exception("Wrong state");
            _needToRebuildMeshFlag = false;
            _needToRebuildMeshNativeArray[0] = false;
            var jobHandle = ImpostorsChunkMesh.ScheduleMeshCreation(dependsOn);
            return jobHandle;
        }

        public Mesh GetMesh()
        {
            var mesh = ImpostorsChunkMesh.GetMesh();
            return mesh;
        }

        public void RemoveAllImpostorsByInstanceId(int impostorLodGroupInstanceId, int indexFrom,
            int indexTo)
        {
            if (impostorLodGroupInstanceId == 0)
                throw new ArgumentOutOfRangeException(nameof(impostorLodGroupInstanceId), "Value must be not zero.");
            _removeInfoArray.Add(new RemoveInfo
            {
                instanceId = impostorLodGroupInstanceId,
                fromIndex = indexFrom,
                toIndex = indexTo
            });
            _needToRebuildMeshFlag = true;
        }

        public void BeginRendering(CommandBuffer cb)
        {
            cb.SetRenderTarget(_renderTexture);
            if (_needToClearTexture)
            {
                cb.SetViewport(new Rect(0, 0, _renderTexture.width, _renderTexture.height));
                cb.ClearRenderTarget(true, true, Color.clear);
                _needToClearTexture = false;
            }
        }

        public void AddCommandBufferCommands(int placeInChunk, CommandBuffer cb)
        {
            cb.SetViewport(GetPixelRect(placeInChunk));
        }

        public void EndRendering(CommandBuffer cb)
        {
            if (_useMipMap)
                cb.GenerateMips(_renderTexture);
        }

        private Rect GetPixelRect(int placeInChunk)
        {
            int x = placeInChunk / _size * TextureResolution;
            int y = placeInChunk % _size * TextureResolution;
            return new Rect(x, y, TextureResolution, TextureResolution);
        }

        private Vector4 GetUV(int placeInChunk)
        {
            int x = placeInChunk / _size;
            int y = placeInChunk % _size;
            return new Vector4(x, y, x + 1, y + 1) / _size;
        }

        public Material GetMaterial()
        {
            return _material;
        }

        [BurstCompile]
        private struct UpdateImpostorsJob : IJobFor
        {
            [ReadOnly]
            public NativeArray<SharedData> sharedDataArray;
            public NativeArray<Impostor> impostors;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeStack<int> emptyPlaces;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<bool> needRebuildMesh;

            public float time;

            public void Execute(int index)
            {
                var impostor = impostors[index];
                if (!impostor.Exists)
                    return;

                if (sharedDataArray[impostor.indexInManagers].data.isPositionChanged)
                {
                    impostor.position = sharedDataArray[impostor.indexInManagers].data.position;
                    needRebuildMesh[0] = true;
                    impostors[index] = impostor;
                }

                if (impostor.isRelevant)
                    return;

                // if impostor is only fading-in and is not ready to fade-out
                if (impostor.time > time)
                {
                    return;
                }

                // when impostor is fully faded-in -> start fade-out
                if (impostor.time > 0)
                {
                    impostor.time = -(time + impostor.fadeTime);
                    impostors[index] = impostor;
                    needRebuildMesh[0] = true;
                    return;
                }

                // when impostor is fully faded-out -> delete it
                if (impostor.time > -time)
                {
                    impostor.impostorLODGroupInstanceId = 0;
                    impostors[index] = impostor;
                    emptyPlaces.Push(index);
                    needRebuildMesh[0] = true;
                }
            }
        }

        [BurstCompile]
        private struct RemoveSpecifiedImpostors : IJobFor
        {
            public NativeArray<Impostor> impostors;

            [WriteOnly]
            [NativeDisableContainerSafetyRestriction]
            public NativeStack<int> emptyPlaces;

            [ReadOnly]
            public NativeList<RemoveInfo> removeInfoArray;

            public void Execute(int index)
            {
                var impostor = impostors[index];
                
                if (!impostor.Exists)
                    return;
                
                for (int i = 0; i < removeInfoArray.Length; i++)
                {
                    if (impostor.impostorLODGroupInstanceId == removeInfoArray[i].instanceId)
                    {
                        impostor.impostorLODGroupInstanceId = 0;
                        emptyPlaces.Push(index);
                        impostors[index] = impostor;
                        return;
                    }

                    if (impostor.indexInManagers == removeInfoArray[i].fromIndex)
                    {
                        impostor.indexInManagers = removeInfoArray[i].toIndex;
                        impostors[index] = impostor;
                    }
                }
            }
        }

        [BurstCompile]
        private struct ClearInstanceIdsJob : IJob
        {
            public NativeList<RemoveInfo> list;

            public void Execute()
            {
                list.Clear();
            }
        }

        public void Dispose()
        {
            _impostors.Dispose();
            _removeInfoArray.Dispose();
            _emptyPlaces.Dispose();
            _needToRebuildMeshNativeArray.Dispose();
            _renderTexturePool.Return(_renderTexture);
            _materialObjectPool.Return(_material);
            _renderTexture = null;
            _material = null;
            ImpostorsChunkMesh.Dispose();
        }

        public int GetUsedBytes()
        {
            var res = 0;

            res += MemoryUsageUtility.GetMemoryUsage(_impostors);
            res += MemoryUsageUtility.GetMemoryUsage(_emptyPlaces);
            res += MemoryUsageUtility.GetMemoryUsage(_removeInfoArray);

            return res;
        }

        // returns the power of desired value. 32 -> 5, 512 -> 9
        private static int InversePowerOfTwo(int value)
        {
            int power = 0;
            while (value > 1)
            {
                power++;
                value = value >> 1;
            }

            return power;
        }
    }
}