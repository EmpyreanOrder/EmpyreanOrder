using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Impostors.Utilities
{
    /// <summary>
    /// You can use this script as it is, or modify it to meed your needs.
    /// </summary>
    public class TerrainTreesToSceneObjectsConverter : MonoBehaviour
    {
        [Header("This script is part of 'Impostors - Runtime Optimization' package.")]
        [Tooltip("Reference to terrain whose trees need to convert.")]
        [SerializeField]
        private Terrain _terrain = default;

        [Tooltip("Parent transform for all spawned trees.")]
        [SerializeField]
        private Transform _treesParent = default;

        [Tooltip("Determines whether conversion will happen automatically on Awake call. Otherwise, you need to manually start conversion process.")]
        [SerializeField]
        private bool _convertOnAwake = true;

        [SerializeField]
        private HideFlags _hideFlagsForCreatedTrees = HideFlags.HideAndDontSave;

        [Header("Load distribution settings")]
        [Tooltip("Determines whether all trees should spawn immediately. Otherwise, settings below control spawn distribution over frames.")]
        [SerializeField]
        private bool _spawnImmediately = false;

        [Tooltip(
            "All trees will be sorted by distance to this point. Nearest one will be spawned first. In most cases this should be a reference to player's spawn point or camera.")]
        [SerializeField]
        private Transform _referencePoint;

        [Range(10, 2000f)]
        [Tooltip("Determines radius around reference point where trees will spawn faster.")]
        [SerializeField]
        private float _nearbyDistance = 500f;

        [Tooltip("Determines how many spawns per frame should happen for nearby trees. Should be bigger than FarawaySpawnsPerFrame")]
        [SerializeField]
        private int _nearbySpawnsPerFrame = 200;

        [Tooltip("Determines how many spawns per frame should happen for trees out of nearby radius. Should be lower than NearbySpawnsPerFrame")]
        [SerializeField]
        private int _farawaySpawnsPerFrame = 30;

        private Action<GameObject> _onTreeInstantiated;

        private void Awake()
        {
            if (_convertOnAwake)
                StartCoroutine(Convert());
        }

        private void Reset()
        {
            _terrain = GetComponent<Terrain>();
            if (_terrain != null)
                _treesParent = _terrain.transform;
        }

        /// <summary>
        /// Converts terrain trees into scene objects using load distribution settings specified in the inspector. 
        /// </summary>
        /// <returns>IEnumerator that should be started using StartCoroutine</returns>
        public IEnumerator Convert()
        {
            if (_spawnImmediately)
            {
                ConvertImmediately(DefaultOnTreeSpawnedCallback);
                yield break;
            }

            yield return ConvertEnumerator(GetReferencePosition(), _nearbyDistance, _nearbySpawnsPerFrame, _farawaySpawnsPerFrame,
                DefaultOnTreeSpawnedCallback);
        }

        /// <summary>
        /// Converts terrain trees into scene objects immediately without load distribution.
        /// </summary>
        /// <param name="onTreeSpawnedCallback"></param>
        public void ConvertImmediately(Action<GameObject> onTreeSpawnedCallback)
        {
            var totalStopwatch = Stopwatch.StartNew();
            DisableTerrainTreesRendering();

            GetTreesParent();

            var treePrefabs = GetTreePrefabs();

            var terrainPosition = _terrain.transform.position;
            var terrainSize = _terrain.terrainData.size;

            var instances = _terrain.terrainData.treeInstances;

            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                var prefab = treePrefabs[instance.prototypeIndex];
                SpawnTreeInstance(instance, terrainSize, terrainPosition, prefab, onTreeSpawnedCallback);
            }

            foreach (var prefab in treePrefabs)
            {
                Destroy(prefab);
            }

            Debug.Log($"[TerrainTreesToSceneObjectsConverter] Spawned {instances.Length} trees in {totalStopwatch.Elapsed.Milliseconds}ms.");
        }

        /// <summary>
        /// Converts terrain trees into scene objects using load distribution settings passed as parameters. Nearest trees are spawned first.
        /// </summary>
        /// <param name="referencePosition">trees are sorted by distance to this position</param>
        /// <param name="nearbyDistance">radius around reference point in which trees spawn faster</param>
        /// <param name="nearbySpawnsPerFrame">spawns per frame for trees inside nearbyDistance radius</param>
        /// <param name="farawaySpawnsPerFrame">spawns per frame for trees outside nearbyDistance radius</param>
        /// <param name="onTreeSpawnedCallback">called for each spawned tree, by default trees are disabled game objects</param>
        /// <returns>IEnumerator that should be started using StartCoroutine</returns>
        public IEnumerator ConvertEnumerator(Vector3 referencePosition, float nearbyDistance, int nearbySpawnsPerFrame, int farawaySpawnsPerFrame,
            Action<GameObject> onTreeSpawnedCallback)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var startFrame = Time.frameCount;
            DisableTerrainTreesRendering();

            GetTreesParent();

            var treePrefabs = GetTreePrefabs();

            var terrainPosition = _terrain.transform.position;
            var terrainSize = _terrain.terrainData.size;

            var instances = _terrain.terrainData.treeInstances;

            var sharedArray = CreateNativeAlias<TreeInstance, TreeInstanceNative>(instances);
#if UNITY_EDITOR
            var safetyHandle = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref sharedArray, safetyHandle);
#endif
            
            var stopwatch = Stopwatch.StartNew();
            var distjob = new CalculateTreeInstanceDistanceJob()
            {
                trees = sharedArray,
                nearCountArr = new NativeArray<int>(1, Allocator.TempJob),
                nearDistance = nearbyDistance,
                referencePosition = referencePosition,
                terrainPosition = terrainPosition,
                terrainSize = terrainSize,
            };
            distjob.Run();
            var nearCount = distjob.nearCountArr[0];
            distjob.nearCountArr.Dispose();

            Debug.Log(
                $"[TerrainTreesToSceneObjectsConverter] Calculated distances for {instances.Length} trees in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
            NativeSortExtension.Sort(sharedArray);
            Debug.Log($"[TerrainTreesToSceneObjectsConverter] Sorted {instances.Length} trees in {stopwatch.ElapsedMilliseconds}ms");

            int _spawnsPerFrame = nearbySpawnsPerFrame;

            for (int i = 0; i < instances.Length; i++)
            {
                if (i % _spawnsPerFrame == 0)
                    yield return null;
                var instance = instances[i];
                var prefab = treePrefabs[instance.prototypeIndex];
                SpawnTreeInstance(instance, terrainSize, terrainPosition, prefab, onTreeSpawnedCallback);

                if (i >= nearCount)
                    _spawnsPerFrame = farawaySpawnsPerFrame;
            }

#if UNITY_EDITOR
            AtomicSafetyHandle.CheckDeallocateAndThrow(safetyHandle);
            AtomicSafetyHandle.Release(safetyHandle);
#endif

            foreach (var prefab in treePrefabs)
            {
                Destroy(prefab);
            }

            Debug.Log(
                $"[TerrainTreesToSceneObjectsConverter] Completed spawning of {instances.Length} trees in {totalStopwatch.Elapsed.TotalSeconds}s, {Time.frameCount - startFrame} frames.");
        }

        private void SpawnTreeInstance(TreeInstance instance, Vector3 terrainSize, Vector3 terrainPosition, GameObject prefab,
            Action<GameObject> onTreeSpawnedCallback)
        {
            var rotation = Quaternion.Euler(0, instance.rotation * Mathf.Rad2Deg, 0);
            var position = instance.position;
            position.x *= terrainSize.x;
            position.y *= terrainSize.y;
            position.z *= terrainSize.z;
            position += terrainPosition;
            var tree = Instantiate(prefab, position, rotation, _treesParent).transform;
            var scale = tree.localScale;
            scale.x *= instance.widthScale;
            scale.y *= instance.heightScale;
            scale.z *= instance.widthScale;
            tree.localScale = scale;
            tree.gameObject.hideFlags = _hideFlagsForCreatedTrees;
            onTreeSpawnedCallback.Invoke(tree.gameObject);
        }

        private void DefaultOnTreeSpawnedCallback(GameObject tree)
        {
            // Here you can add logic to reference tree in some kind of sectors system or world streaming system for big scenes.
            // Such system activates/deactivates object based on its distance from player. For example, quadrant system.
            // Best practice:
            // It's preferable to not call tree.gameObject.SetActive(true) before passing tree into such system,
            // because there are some heavy calculations in Awake and OnEnable that potentially won't be needed
            // if object is too far and will be disabled right away.
            tree.gameObject.SetActive(true);
        }

        private GameObject[] GetTreePrefabs()
        {
            var terrainData = _terrain.terrainData;

            var prototypes = terrainData.treePrototypes;

            var treePrefabs = new GameObject[prototypes.Length];
            for (var i = 0; i < prototypes.Length; i++)
            {
                var treePrototype = prototypes[i];
                var prefab = Instantiate(treePrototype.prefab);
                prefab.SetActive(false);
                prefab.name = treePrototype.prefab.name;
                treePrefabs[i] = prefab;
            }

            return treePrefabs;
        }

        private void GetTreesParent()
        {
            if (_treesParent == null)
                _treesParent = _terrain.transform;
        }

        private void DisableTerrainTreesRendering()
        {
            _terrain.treeDistance = 0;
            _terrain.treeBillboardDistance = float.MaxValue;
            _terrain.treeMaximumFullLODCount = 0;
        }

        private static unsafe NativeArray<TNative> CreateNativeAlias<T, TNative>(T[] m_Managed)
            where T : unmanaged where TNative : unmanaged
        {
            NativeArray<TNative> m_Native;
            // this is the trick to making a NativeArray view of a managed array (or any pointer)
            fixed (void* ptr = m_Managed)
            {
                m_Native = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TNative>
                    (ptr, m_Managed.Length, Allocator.None);
            }

            return m_Native;
        }

        private Vector3 GetReferencePosition()
        {
            if (_referencePoint != null)
                return _referencePoint.position;
            if (Camera.main != null)
                return Camera.main.transform.position;
            return Vector3.zero;
        }

        [BurstCompile]
        private struct CalculateTreeInstanceDistanceJob : IJob
        {
            // TODO
            [NativeDisableContainerSafetyRestriction]
            [NativeDisableUnsafePtrRestriction]
            public NativeArray<TreeInstanceNative> trees;

            public float nearDistance;
            public float3 terrainSize;
            public float3 terrainPosition;
            public float3 referencePosition;
            public NativeArray<int> nearCountArr;

            public void Execute()
            {
                var nearDistSqr = nearDistance * nearDistance;
                var nearCount = 0;
                for (int i = 0; i < trees.Length; i++)
                {
                    var tree = trees[i];
                    float3 position = tree.position;
                    position.x *= terrainSize.x;
                    position.y *= terrainSize.y;
                    position.z *= terrainSize.z;
                    position += terrainPosition;
                    var distSqr = math.lengthsq(referencePosition - position);
                    tree.distance = distSqr;
                    nearCount += distSqr < nearDistSqr ? 1 : 0;
                    trees[i] = tree;
                }

                nearCountArr[0] = nearCount;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TreeInstanceNative : IComparable<TreeInstanceNative>
        {
            /// <summary>
            ///   <para>Position of the tree.</para>
            /// </summary>
            public float3 position;

            /// <summary>
            ///   <para>Width scale of this instance (compared to the prototype's size).</para>
            /// </summary>
            public float widthScale;

            /// <summary>
            ///   <para>Height scale of this instance (compared to the prototype's size).</para>
            /// </summary>
            public float heightScale;

            /// <summary>
            ///         <para>Read-only.
            /// 
            /// Rotation of the tree on X-Z plane (in radians).</para>
            ///       </summary>
            public float rotation;

            /// <summary>
            ///   <para>Color of this instance.</para>
            /// </summary>
            public Color32 color;

            /// <summary>
            ///   <para>Lightmap color calculated for this instance.</para>
            /// </summary>
            public Color32 lightmapColor;

            /// <summary>
            ///   <para>Index of this instance in the TerrainData.treePrototypes array.</para>
            /// </summary>
            public int prototypeIndex;

            public float distance;

            public int CompareTo(TreeInstanceNative other)
            {
                return distance.CompareTo(other.distance);
            }
        }
    }
}