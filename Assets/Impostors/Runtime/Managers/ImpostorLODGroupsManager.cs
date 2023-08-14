using System;
using System.Collections.Generic;
using Impostors.Attributes;
using Impostors.Jobs;
using Impostors.MemoryUsage;
using Impostors.ObjectPools;
using Impostors.Structs;
using Impostors.TimeProvider;
using Unity.Collections;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
using UnityEngine.Serialization;

namespace Impostors.Managers
{
    [DefaultExecutionOrder(-777)]
    public class ImpostorLODGroupsManager : MonoBehaviour, IMemoryConsumer
    {
        public static ImpostorLODGroupsManager Instance { get; private set; }
        private const float PositionChangeDelta = 0.01f;

        public ITimeProvider TimeProvider { get; private set; }

        [Tooltip("Enable this if you want imposters to render HDR values e.g. materials with emission. " +
                 "Disabled by default because consumes noticeably more memory. ")]
        [SerializeField, DisableAtRuntime]
        private bool _HDR = false;

        [Tooltip("Enables mipmaps on imposter textures. Enhances look of distant imposters. " +
                 "Enabled by default, doesn’t affect performance that much.")]
        [SerializeField, DisableAtRuntime]
        private bool _useMipMap = false;

        [Tooltip("Sets mipmap bias to all imposter textures. " +
                 "If distant imposters looks blurrish try set slightly negative value like -0.25. By default 0.")]
        [SerializeField, DisableAtRuntime]
        private float _mipMapBias = 0;

        [FormerlySerializedAs("cutout")]
        [Tooltip("Sets global cutout applied to all imposters. " +
                 "Increase to make imposter’s edges sharper. Does almost nothing when mipmaps are disabled.")]
        [FormerlySerializedAs("_cutout")]
        [Range(0f, 1f)]
        [SerializeField]
        public float cutoff = 0.02f;

        [Tooltip("Increase this value if imposters are weirdly rotating when camera look down from above.")]
        [FormerlySerializedAs("_minAngleToStopLookAtCamera")]
        [Range(0f, 180f)]
        [SerializeField]
        public float minAngleToStopLookAtCamera = 30;

        [Tooltip("Shader that will be used to draw all imposters.")]
        [SerializeField, DisableAtRuntime]
        private Shader _shader = default;

        [Tooltip("Pattern that controls how imposters crossfade.")]
        [SerializeField]
        private Texture _ditherTexture = default;

        [Space]
        [Header("Runtime")]
        public CompositeRenderTexturePool RenderTexturePool;

        public MaterialObjectPool MaterialObjectPool;

        public JobHandle SyncTransformsJobHandle { get; private set; }

        [SerializeField]
        private List<CameraImpostorsManager> _impostorsManagers = default;

        [SerializeField]
        private List<ImpostorLODGroup> _impostorLodGroups = default;

        private Dictionary<int, ImpostorLODGroup> _dictInstanceIdToImpostorLODGroup;

        private NativeList<SharedData> _sharedDataList;
        private TransformAccessArray _transformAccessArray;

        private bool _isDestroying = false;
        private bool _isRequestedTransformDataUpdateForStaticImpostors = false;

        private void OnEnable()
        {
            AllocateNativeCollections();
            _isDestroying = false;
            Instance = this;
            _impostorLodGroups = new List<ImpostorLODGroup>();
            _impostorsManagers = _impostorsManagers ?? new List<CameraImpostorsManager>();
            if (!_shader)
                _shader = Shader.Find("Hidden/Impostors/ImpostorsShader");
            _dictInstanceIdToImpostorLODGroup = new Dictionary<int, ImpostorLODGroup>();
            TimeProvider = new ScaledTimeProvider();
            RenderTexturePool = new CompositeRenderTexturePool(
                Enum.GetValues(typeof(AtlasResolution)) as int[], 0, 16,
                _useMipMap, _mipMapBias, GetRenderTextureFormat());
            MaterialObjectPool = new MaterialObjectPool(0, _shader);
        }

        private void OnDisable()
        {
            _isDestroying = true;
            DisposeNativeCollections();
        }

        private void AllocateNativeCollections()
        {
            _sharedDataList = new NativeList<SharedData>(1024, Allocator.Persistent);
            _transformAccessArray = new TransformAccessArray(1024);
        }

        private void DisposeNativeCollections()
        {
            SyncTransformsJobHandle.Complete();
            _sharedDataList.Dispose();
            _transformAccessArray.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_ditherTexture == null)
            {
                _ditherTexture = Resources.Load<Texture>("impostors-dither-pattern");
                EditorUtility.SetDirty(this);
            }

            if (_shader == null)
            {
                _shader = Shader.Find("Hidden/Impostors/ImpostorsShader");
                EditorUtility.SetDirty(this);
            }

            if (!_ditherTexture)
                Debug.LogError("[IMPOSTORS] Impostors fading won't work without specifying dither pattern texture! " +
                               "Default path is 'Assets/Impostors/Runtime/Resources/impostors-dither-pattern.png'.",
                    this);
            if (!_shader)
                Debug.LogError("[IMPOSTORS] Impostors won't work without specifying right shader! " +
                               "Default path is 'Assets/Impostors/Runtime/Resources/Shaders/ImpostorsShader.shader'.",
                    this);
        }

        private void Reset()
        {
            _ditherTexture = Resources.Load<Texture>("impostors-dither-pattern");
            _shader = Shader.Find("Hidden/Impostors/ImpostorsShader");
        }
#endif

        private void Update()
        {
            TimeProvider.Update();
            Shader.SetGlobalVector(ShaderProperties._ImpostorsTimeProvider,
                new Vector4(TimeProvider.Time, TimeProvider.DeltaTime, 0, 0));
            Shader.SetGlobalTexture(ShaderProperties._ImpostorsNoiseTexture, _ditherTexture);
            Shader.SetGlobalFloat(ShaderProperties._ImpostorsNoiseTextureResolution, _ditherTexture.width);
            Shader.SetGlobalFloat(ShaderProperties._ImpostorsCutoff, cutoff);
            Shader.SetGlobalFloat(ShaderProperties._ImpostorsMinAngleToStopLookAt, minAngleToStopLookAtCamera);
        }

        private void LateUpdate()
        {
            SyncTransformsJobHandle.Complete();

            var syncTransformsJob = new SyncSharedDataWithTransformDataJob()
            {
                forceUpdateForStatic = _isRequestedTransformDataUpdateForStaticImpostors,
                positionChangeDeltaSquared = PositionChangeDelta * PositionChangeDelta,
                sharedDataArray = _sharedDataList
            };
            SyncTransformsJobHandle = syncTransformsJob.Schedule(_transformAccessArray);
            _isRequestedTransformDataUpdateForStaticImpostors = false;
        }

        public int AddImpostorLODGroup(ImpostorLODGroup impostorLodGroup)
        {
            if (_isDestroying)
                return -1;
            SyncTransformsJobHandle.Complete();
            _impostorLodGroups.Add(impostorLodGroup);
            _transformAccessArray.Add(impostorLodGroup.transform);
            int instanceId = impostorLodGroup.GetInstanceID();
            _sharedDataList.Add(new SharedData
            {
                impostorLODGroupInstanceId = instanceId,
                indexInManagers = _impostorLodGroups.Count - 1
            });
            _dictInstanceIdToImpostorLODGroup.Add(instanceId, impostorLodGroup);

            for (int i = 0; i < _impostorsManagers.Count; i++)
            {
                _impostorsManagers[i].AddImpostorableObject(impostorLodGroup);
            }

            impostorLodGroup.IndexInImpostorsManager = _impostorLodGroups.Count - 1;
            UpdateSettings(impostorLodGroup);
            return _impostorLodGroups.Count - 1;
        }

        public void UpdateSettings(ImpostorLODGroup impostorLODGroup)
        {
            SyncTransformsJobHandle.Complete();
            
            int index = impostorLODGroup.IndexInImpostorsManager;

            var sharedData = _sharedDataList[index];

            sharedData.data.position = impostorLODGroup.Position;
            sharedData.data.localReferencePoint = impostorLODGroup.LocalReferencePoint;
            sharedData.data.forward = impostorLODGroup.transform.forward;
            sharedData.data.size = impostorLODGroup.Size;
            sharedData.data.height = impostorLODGroup.LocalHeight;
            sharedData.data.quadSize = impostorLODGroup.QuadSize;
            sharedData.data.zOffset = impostorLODGroup.ZOffsetWorld;
            sharedData.settings.isStatic = impostorLODGroup.isStatic;
            sharedData.settings.fadeInTime = impostorLODGroup.FadeInTime;
            sharedData.settings.fadeOutTime = impostorLODGroup.FadeOutTime;
            sharedData.settings.fadeTransitionTime = impostorLODGroup.fadeTransitionTime;
            sharedData.settings.deltaCameraAngle = impostorLODGroup.deltaCameraAngle;
            sharedData.settings.useUpdateByTime = (byte) (impostorLODGroup.useUpdateByTime ? 1 : 0);
            sharedData.settings.timeInterval = impostorLODGroup.timeInterval;
            sharedData.settings.useDeltaLightAngle = (byte) (impostorLODGroup.useDeltaLightAngle ? 1 : 0);
            sharedData.settings.deltaLightAngle = impostorLODGroup.deltaLightAngle;
            sharedData.settings.deltaDistance = impostorLODGroup.deltaDistance;
            sharedData.settings.minTextureResolution = (int) impostorLODGroup.minTextureResolution;
            sharedData.settings.maxTextureResolution = (int) impostorLODGroup.maxTextureResolution;
            sharedData.settings.screenRelativeTransitionHeight =
                impostorLODGroup.ScreenRelativeTransitionHeight;
            sharedData.settings.screenRelativeTransitionHeightCull =
                impostorLODGroup.ScreenRelativeTransitionHeightCull;

            _sharedDataList[index] = sharedData;
        }

        public void RemoveImpostorLODGroup(ImpostorLODGroup impostorLodGroup)
        {
            if (_isDestroying)
                return;
            SyncTransformsJobHandle.Complete();
            int index = impostorLodGroup.IndexInImpostorsManager;
            Assert.AreEqual(impostorLodGroup, _impostorLodGroups[index]);
            _transformAccessArray.RemoveAtSwapBack(index);

            // Remove from SharedData list
            {
                _sharedDataList.RemoveAtSwapBack(index);

                if (index < _sharedDataList.Length)
                {
                    var sharedData = _sharedDataList[index];
                    sharedData.indexInManagers = index;
                    _sharedDataList[index] = sharedData;
                }
            }

            // Remove from ImpostorLODGroup list
            {
                if (index < _impostorLodGroups.Count - 1)
                {
                    _impostorLodGroups[index] = _impostorLodGroups[_impostorLodGroups.Count - 1];
                    _impostorLodGroups[index].IndexInImpostorsManager = index;
                }

                _impostorLodGroups.RemoveAt(_impostorLodGroups.Count - 1);
            }


            for (int i = 0; i < _impostorsManagers.Count; i++)
            {
                _impostorsManagers[i].RemoveImpostorableObject(impostorLodGroup, index);
            }

            _dictInstanceIdToImpostorLODGroup.Remove(impostorLodGroup.GetInstanceID());
        }

        internal void RegisterCameraImpostorsManager(CameraImpostorsManager manager)
        {
            if (_impostorsManagers.Contains(manager))
                return;
            _impostorsManagers.Add(manager);

            for (int i = 0; i < _impostorLodGroups.Count; i++)
            {
                manager.AddImpostorableObject(_impostorLodGroups[i]);
            }
        }

        internal void UnregisterCameraImpostorsManager(CameraImpostorsManager manager)
        {
            _impostorsManagers.Remove(manager);
        }

        public ImpostorLODGroup GetByInstanceId(int instanceId)
        {
            return _dictInstanceIdToImpostorLODGroup[instanceId];
        }

        public void RequestImpostorTextureUpdate(ImpostorLODGroup impostorLODGroup)
        {
            for (int i = 0; i < _impostorsManagers.Count; i++)
            {
                _impostorsManagers[i].RequestImpostorTextureUpdate(impostorLODGroup);
            }
        }

        public void RequestTransformDataUpdateForStaticImpostors()
        {
            _isRequestedTransformDataUpdateForStaticImpostors = true;
        }
        
        public int GetUsedBytes()
        {
            int res = 0;
            res += MemoryUsageUtility.GetMemoryUsage(_impostorsManagers);
            res += MemoryUsageUtility.GetMemoryUsage(_impostorLodGroups);
            res += _dictInstanceIdToImpostorLODGroup.Count * (8 + 4);

            foreach (var impostorableObjectsManager in _impostorsManagers)
            {
                res += impostorableObjectsManager.GetUsedBytes();
            }

            return res;
        }

        private RenderTextureFormat GetRenderTextureFormat()
        {
            if (_HDR)
            {
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                    return RenderTextureFormat.ARGBHalf;

                Debug.LogError(
                    $"[IMPOSTORS] Current system doesn't support '{RenderTextureFormat.ARGBHalf}' render texture format. " +
                    $"Falling back to default, non HDR textures.");
            }

            return RenderTextureFormat.Default;
        }

        internal SharedData GetSharedData(int index)
        {
            return _sharedDataList[index];
        }

        internal NativeArray<SharedData> GetSharedDataArray()
        {
            return _sharedDataList.AsArray();
        }
    }
}