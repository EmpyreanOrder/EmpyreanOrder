// Copyright (c) 2014 Make Code Now! LLC

// Unity does not (currently) respect enabled property
// or the layer property for all renderable objects, esp
// when there are multiple cameras in the scene. An alternative,
// is to use Layers and a culling mask, but those don't work
// universally either. Current behavior (in 4.3) is:
// * Mesh can cull with layer or enabled, but enabled only works on first camera.
// * Light only works with enabled, layer seems to still have full costs.
// * Terrain only works with layer, enabled seems to have no effect unless set earlier than pre-cull.

#if !UNITY_WP8 && !UNITY_WP8_1 && !UNITY_WSA
#define THREAD_CULLING
#endif

using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.Rendering;
#elif UNITY_2018_3_OR_NEWER
using UnityEngine.Experimental.Rendering;
#endif

/// \ingroup Vis
/// CullingCamera is the workhorse of SECTR Vis, culling objects by propagating Camera
/// data down through the Sector/Portal graph and into individual SECTR_Culler objects.
///
/// Culling in SECTR is a fairly straightforward process. Each CullingCamera is expected to
/// have a sibling Unity Camera. This allows PreCull() to be called, which is where our Camera
/// does its work. CullingCamera cleans up after itself in PostRender, which allows multiple SECTR Cameras
/// to be active in a single scene at once (if so desired).
/// 
/// Culling starts with the Sector(s) that contain the current Camera. From there, the
/// CullingCamera walks the Sector graph. At each Portal, the Camera tests
/// to see if its view frustum intersects the Portal's geometry. If it does, the frustum
/// is clipped down by the Portal geometry, and the traversal continues to the next Sector
/// (in a depth-first manner). Eventually, the frustum is winowed down to the point where no
/// additional Portals are visible and the traversal completes.
/// 
/// SECTR Vis also allows the use of culling via instances of SECTR_Occluder. As the CullingCamera walks
/// the Sector/Portal graph, it accumulates any Occluders that are present in that Sector. All future objects
/// are then tested against the accumulated Occluders.
/// 
/// Lastly, shadow casting lights are accumulated during the traversal. Because of the complexities
/// of shadow casting lights effectively extending the bounds of shadow casting meshes into Sectors
/// that they would not otherwise occupy, the CullingCamera accumulates shadow casting point lights during
/// the main traversal and then performs a post-pass for on any relevant meshes to ensure shadows are
/// never prematurely culled.
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Procedural Worlds/SECTR/Vis/SECTR Culling Camera")]
public class SECTR_CullingCamera : MonoBehaviour
{
#region Private Details
    /// A node of the Sector/Portal graph as relevant to the culling process.
    private struct VisibilityNode
    {
        public VisibilityNode(SECTR_CullingCamera cullingCamera, SECTR_Sector sector, SECTR_Portal portal, Plane[] frustumPlanes, bool forwardTraversal)
        {
            this.sector = sector;
            this.portal = portal;
            if (frustumPlanes == null)
            {
                this.frustumPlanes = null;
            }
            else if (cullingCamera.frustumPool.Count > 0)
            {
                this.frustumPlanes = cullingCamera.frustumPool.Pop();
                this.frustumPlanes.AddRange(frustumPlanes);
            }
            else
            {
                this.frustumPlanes = new List<Plane>(frustumPlanes);
            }
            this.forwardTraversal = forwardTraversal;
        }

        public VisibilityNode(SECTR_CullingCamera cullingCamera, SECTR_Sector sector, SECTR_Portal portal, List<Plane> frustumPlanes, bool forwardTraversal)
        {
            this.sector = sector;
            this.portal = portal;
            if (frustumPlanes == null)
            {
                this.frustumPlanes = null;
            }
            else if (cullingCamera.frustumPool.Count > 0)
            {
                this.frustumPlanes = cullingCamera.frustumPool.Pop();
                this.frustumPlanes.AddRange(frustumPlanes);
            }
            else
            {
                this.frustumPlanes = new List<Plane>(frustumPlanes);
            }
            this.forwardTraversal = forwardTraversal;
        }

        public SECTR_Sector sector;
        public SECTR_Portal portal;
        public List<Plane> frustumPlanes;
        public bool forwardTraversal;
    };

    /// An expanded vertex structure used for clipping SectorPortal geometry.
    private struct ClipVertex
    {
        public ClipVertex(Vector4 vertex, float side)
        {
            this.vertex = vertex;
            this.side = side;
        }

        public Vector4 vertex;
        public float side;
    }

#if THREAD_CULLING
    private struct ThreadCullData
    {
        public enum CullingModes
        {
            None,
            Graph,
            Shadow,
        };

        public ThreadCullData(SECTR_Sector sector,
                              SECTR_CullingCamera cullingCamera,
                              Vector3 cameraPos,
                              List<Plane> cullingPlanes,
                              List<List<Plane>> occluderFrustums,
                              int baseMask,
                              float shadowDistance,
                              bool cullingSimpleCulling)
        {
            this.sector = sector;
            this.cameraPos = cameraPos;
            this.baseMask = baseMask;
            this.shadowDistance = shadowDistance;
            this.cullingSimpleCulling = cullingSimpleCulling;
            this.sectorShadowLights = null;

            lock (cullingCamera.threadOccluderPool)
            {
                this.occluderFrustums = cullingCamera.threadOccluderPool.Count > 0 ? cullingCamera.threadOccluderPool.Pop() : new List<List<Plane>>(occluderFrustums.Count);
            }

            lock (cullingCamera.threadFrustumPool)
            {
                if (cullingCamera.threadFrustumPool.Count > 0)
                {
                    this.cullingPlanes = cullingCamera.threadFrustumPool.Pop();
                    this.cullingPlanes.AddRange(cullingPlanes);
                }
                else
                {
                    this.cullingPlanes = new List<Plane>(cullingPlanes);
                }

                int numOccluders = occluderFrustums.Count;
                for (int occluderIndex = 0; occluderIndex < numOccluders; ++occluderIndex)
                {
                    List<Plane> occluderFrustum = null;
                    if (cullingCamera.threadFrustumPool.Count > 0)
                    {
                        occluderFrustum = cullingCamera.threadFrustumPool.Pop();
                        occluderFrustum.AddRange(occluderFrustums[occluderIndex]);
                    }
                    else
                    {
                        occluderFrustum = new List<Plane>(occluderFrustums[occluderIndex]);
                    }
                    this.occluderFrustums.Add(occluderFrustum);
                }
            }
            cullingMode = CullingModes.Graph;
        }

        public ThreadCullData(SECTR_Sector sector, Vector3 cameraPos, List<SECTR_Member.Child> sectorShadowLights)
        {
            this.sector = sector;
            this.cameraPos = cameraPos;
            this.cullingPlanes = null;
            this.occluderFrustums = null;
            this.baseMask = 0;
            this.shadowDistance = 0;
            this.cullingSimpleCulling = false;
            this.sectorShadowLights = sectorShadowLights; // Does not need to be a copy because only this thread will access the Array.
            cullingMode = CullingModes.Shadow;
        }

        public SECTR_Sector sector;
        public Vector3 cameraPos;
        public List<Plane> cullingPlanes;
        public List<List<Plane>> occluderFrustums;
        public int baseMask;
        public float shadowDistance;
        public bool cullingSimpleCulling;
        public List<SECTR_Member.Child> sectorShadowLights;
        public CullingModes cullingMode;
    }
#endif

    // Component Cache
    Camera myCamera = null;
    SECTR_Member cullingMember;

    // Lists for keeping track of the items we hid so that we can undo the hiding.
    private Dictionary<int, SECTR_Member.Child> hiddenRenderers = new Dictionary<int, SECTR_Member.Child>(16);
    private Dictionary<int, SECTR_Member.Child> hiddenLights = new Dictionary<int, SECTR_Member.Child>(16);
    private Dictionary<int, SECTR_Member.Child> hiddenTerrains = new Dictionary<int, SECTR_Member.Child>(2);

    // Useful stats
    private int renderersCulled = 0;
    private int lightsCulled = 0;
    private int terrainsCulled = 0;

    // Records if culling was actually performed this frame.
    private bool didCull = false;
    private bool runOnce = false;
    // Bookkeeping data structures
    // Graph Traversal
    private List<SECTR_Sector> initialSectors = new List<SECTR_Sector>(4);
    private Stack<VisibilityNode> nodeStack = new Stack<VisibilityNode>(10);

    // Culling
    private List<ClipVertex> portalVertices = new List<ClipVertex>(16);
    private List<Plane> newFrustum = new List<Plane>(16);
    private List<Plane> cullingPlanes = new List<Plane>(16);

    // Occluders
    private List<List<Plane>> occluderFrustums = new List<List<Plane>>(10);
    private Dictionary<SECTR_Occluder, SECTR_Occluder> activeOccluders = new Dictionary<SECTR_Occluder, SECTR_Occluder>(10);
    private List<ClipVertex> occluderVerts = new List<ClipVertex>(10);

    // Shadow post-pass
    private Dictionary<SECTR_Member.Child, int> shadowLights = new Dictionary<SECTR_Member.Child, int>(10);
    private List<SECTR_Sector> shadowSectors = new List<SECTR_Sector>(4);
    private Dictionary<SECTR_Sector, List<SECTR_Member.Child>> shadowSectorTable = new Dictionary<SECTR_Sector, List<SECTR_Member.Child>>(4);

    // Dicts for keeping track of which objects are definitely visible.
    private Dictionary<int, SECTR_Member.Child> visibleRenderers = new Dictionary<int, SECTR_Member.Child>(1024);
    private Dictionary<int, SECTR_Member.Child> visibleLights = new Dictionary<int, SECTR_Member.Child>(256);
    private Dictionary<int, SECTR_Member.Child> visibleTerrains = new Dictionary<int, SECTR_Member.Child>(32);

    //List to keep track of visible cloth renderers. Those cannot be hidden by default as it would cause issues with the cloth simulation.
    private List<SECTR_Member.Child> visibleClothRenderers = new List<SECTR_Member.Child>();

    private Stack<List<Plane>> frustumPool = new Stack<List<Plane>>(32);
    private Stack<List<SECTR_Member.Child>> shadowLightPool = new Stack<List<SECTR_Member.Child>>(32);
#if THREAD_CULLING
    // Pools of items used by worker threads. Could be combined with similar main thread pools, but
    // kept separate for now just for simplicity and to avoid a bit of extra lock contention.
    private Stack<Dictionary<int, SECTR_Member.Child>> threadVisibleListPool = new Stack<Dictionary<int, SECTR_Member.Child>>(4);
    private Stack<Dictionary<SECTR_Member.Child, int>> threadShadowLightPool = new Stack<Dictionary<SECTR_Member.Child, int>>(32);
    private Stack<List<Plane>> threadFrustumPool = new Stack<List<Plane>>(32);
    private Stack<List<List<Plane>>> threadOccluderPool = new Stack<List<List<Plane>>>(32);
    // The list of worker threads.
    private List<Thread> workerThreads = new List<Thread>();
    // A queue which will store all work requests from the main thread
    // generated during the culling graph walk or shadow post pass.
    private Queue<ThreadCullData> cullingWorkQueue = new Queue<ThreadCullData>(32);
    // The main thread needs to join with the worker threads, but must wait until
    // all of the work is done, not just the work queue being empty. This counter
    // is used in tandem with the queue to indicate when all queued work is finished.
    private int remainingThreadWork = 0;
#endif

    // Cache of all culling cameras in scene.
    private static List<SECTR_CullingCamera> allCullingCameras = new List<SECTR_CullingCamera>(4);

#if UNITY_EDITOR
    // Convenience data structure for rendering debug data in editor.
    private class ClipRenderData
    {
        public ClipRenderData(List<ClipVertex> clippedPortalVerts, bool forward)
        {
            this.clippedPortalVerts = new List<ClipVertex>(clippedPortalVerts);
            this.forward = forward;
        }

        public List<ClipVertex> clippedPortalVerts = null;
        public bool forward = true;
    }

    // For debug visualization
    private List<ClipRenderData> clipPortalData = new List<ClipRenderData>(16);
    private List<ClipRenderData> clipOccluderData = new List<ClipRenderData>(16);
    private Mesh debugFrustumMesh;
    private Mesh debugOccluderMesh;
    private static Color ClippedPortalColor = Color.blue;
    private static Color ClippedOccluderColor = Color.red;
#endif
#endregion

#region Public Interface
    [SECTR_ToolTip("Allows multiple culling cameras to be active at once, but at the cost of some performance.")]
    public bool MultiCameraCulling = true;
    [SECTR_ToolTip("Forces culling into a mode designed for 2D and iso games where the camera is always outside the scene.")]
    public bool SimpleCulling = false;
    [SECTR_ToolTip("Distance to draw clipped frustums.", 0f, 100f)]
    public float GizmoDistance = 10f;
    [SECTR_ToolTip("Material to use to render the debug frustum mesh.")]
    public Material GizmoMaterial = null;
    [SECTR_ToolTip("Makes the Editor camera display the Game view's culling while playing in editor.")]
    public bool CullInEditor = false;
    [SECTR_ToolTip("Set to false to disable shadow culling post pass.", true)]
    public bool CullShadows = true;
    [SECTR_ToolTip("Use another camera for culling properties.", true)]
    public Camera cullingProxy = null;

#if THREAD_CULLING
    [SECTR_ToolTip("Number of worker threads for culling. Do not set this too high or you may see hitching.", 0, -1)]
    public int NumWorkerThreads = 0;
#endif

#if UNITY_2018_3_OR_NEWER
    [SECTR_ToolTip("Enables a workaround when using Scripted Rendering Pipelines (HDRP/URP/LWRP). Keep disabled if using built-in rendering.", 0, -1)]
    public bool SRP_Fix = false;
#endif


    /// Returns a list of all enabled CullingCameras.
    public static List<SECTR_CullingCamera> All
    {
        get { return allCullingCameras; }
    }

    /// Return the number of renderers culled last frame.
    public int RenderersCulled { get { return renderersCulled; } }

    /// Return the number of lights culled last frame.
    public int LightsCulled { get { return lightsCulled; } }

    /// Return the number of lights culled last frame.
    public int TerrainsCulled { get { return terrainsCulled; } }

    /// Resets all stats. Useful for demos.
    public void ResetStats()
    {
        renderersCulled = 0;
        lightsCulled = 0;
        terrainsCulled = 0;
        runOnce = false;
    }
    #endregion

    #region Unity Interface


#if UNITY_2018_3_OR_NEWER

#if UNITY_2019_3_OR_NEWER
    private void RenderPipeline_beginFrameRendering(ScriptableRenderContext ScriptableRenderContext, Camera[] obj)
#else
    private void RenderPipeline_beginFrameRendering(Camera[] obj)
#endif
    {

        OnPreCull();
    }
#endif

    void OnEnable()
    {

#if UNITY_2018_3_OR_NEWER
        if (SRP_Fix)
        {
#if UNITY_2019_3_OR_NEWER
        RenderPipelineManager.beginFrameRendering += RenderPipeline_beginFrameRendering;
#else
        RenderPipeline.beginFrameRendering += RenderPipeline_beginFrameRendering;
#endif
        }
#endif

            myCamera = GetComponent<Camera>();
        cullingMember = GetComponent<SECTR_Member>();

        allCullingCameras.Add(this);
        runOnce = false;

#if THREAD_CULLING
        // We create our own workers to ensure that we have the maximum control
        // of scheduling and life cycle with the minimum of overhead.
        int numWorkers = Mathf.Min(NumWorkerThreads, SystemInfo.processorCount);
        for (int workerIndex = 0; workerIndex < numWorkers; ++workerIndex)
        {
            Thread newThread = new Thread(_CullingWorker);
            newThread.IsBackground = true;
            newThread.Priority = System.Threading.ThreadPriority.Highest;
            newThread.Start();
            workerThreads.Add(newThread);
        }
#endif

#if UNITY_EDITOR
        debugFrustumMesh = new Mesh();
        if (!GizmoMaterial)
        {
            string path = SECTR_SectorUtils.GetSectrDirectory() + SECTR_Constants.PATH_VisGizmoMaterial;
            GizmoMaterial = (Material)(AssetDatabase.LoadAssetAtPath(path, typeof(Material)));
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_2018_3_OR_NEWER
        if (SRP_Fix)
        {
#if UNITY_2019_3_OR_NEWER
            RenderPipelineManager.beginFrameRendering -= RenderPipeline_beginFrameRendering;
#else
            RenderPipeline.beginFrameRendering -= RenderPipeline_beginFrameRendering;
#endif
        }
#endif

            if (!MultiCameraCulling)
        {
            _UndoCulling();
        }

        allCullingCameras.Remove(this);

#if THREAD_CULLING
        // Remove all worker threads. Abort should be safe here since we join during
        // PreCull so no work should be running during OnDisable.
        int numWorkers = workerThreads.Count;
        for (int workerIndex = 0; workerIndex < numWorkers; ++workerIndex)
        {
            workerThreads[workerIndex].Abort();
        }
#endif

#if UNITY_EDITOR
        Mesh.DestroyImmediate(debugFrustumMesh);
        debugFrustumMesh = null;
        GizmoMaterial = null;
#endif
    }

    void OnDestroy()
    {

#if UNITY_2018_3_OR_NEWER
        if (SRP_Fix)
        {
#if UNITY_2019_3_OR_NEWER
            RenderPipelineManager.beginFrameRendering -= RenderPipeline_beginFrameRendering;
#else
            RenderPipeline.beginFrameRendering -= RenderPipeline_beginFrameRendering;
#endif
        }
#endif
#if UNITY_EDITOR
            // Cleanup for the in-editor culling debug-vis stuff.
            Camera sceneCamera = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera : null;
        if (sceneCamera)
        {
            SECTR_CullingCamera sceneCamCuller = sceneCamera.GetComponent<SECTR_CullingCamera>();
            if (sceneCamCuller != null && sceneCamCuller.cullingProxy == myCamera)
            {
                sceneCamCuller.cullingProxy = null;
                sceneCamCuller.enabled = false;

                int numPortals = SECTR_Portal.All.Count;
                for (int portalIndex = 0; portalIndex < numPortals; ++portalIndex)
                {
                    SECTR_Portal portal = SECTR_Portal.All[portalIndex];
                    portal.Hidden = false;
                }
            }
        }
#endif
    }

    void OnPreCull()
    {
        
#if UNITY_EDITOR
        clipPortalData.Clear();
        clipOccluderData.Clear();
#endif
        // Compute the culling camear and some constants that we'll use throughout the walk.
        Camera cullingCamera = cullingProxy != null ? cullingProxy : myCamera;
        Vector3 cameraPos = cullingCamera.transform.position;
        float maxCameraFOVAngle = Mathf.Max(cullingCamera.fieldOfView, cullingCamera.fieldOfView * cullingCamera.aspect) * 0.5f;
        float maxCameraFOV = Mathf.Cos(maxCameraFOVAngle * Mathf.Deg2Rad);
        const float kNEAR_CLIP_SCALE = 1.001f;
        float maxNearClipDistance = Mathf.Max(0,(cullingCamera.nearClipPlane / maxCameraFOV) * kNEAR_CLIP_SCALE);

#if UNITY_EDITOR
        bool fullCulling = (!Application.isEditor || Application.isPlaying);
#endif

        if (cullingProxy)
        {
            SECTR_CullingCamera proxy = cullingProxy.GetComponent<SECTR_CullingCamera>();
            if (proxy)
            {
                SimpleCulling = proxy.SimpleCulling;
                CullShadows = proxy.CullShadows;
                if (MultiCameraCulling != proxy.MultiCameraCulling)
                {
                    runOnce = false;
                }
                MultiCameraCulling = proxy.MultiCameraCulling;
            }
        }

        // Update all LODs up front. This will ensure the member's are update and ready
        // to be culled. Also allows LODs to work without any Sectors setup.
#if UNITY_EDITOR
        if (fullCulling)
#endif
        {
            Profiler.BeginSample("LODs");
            int numLODs = SECTR_LOD.All.Count;
            for (int lodIndex = 0; lodIndex < numLODs; ++lodIndex)
            {
                SECTR_LOD.All[lodIndex].SelectLOD(cullingCamera);
            }
            Profiler.EndSample(); // LODs
        }
        

        // Get all of the Sectors that contain the camera, which can be more than one.
        // If the camera is not inside any Sector at all, no culling is possible, so just skip it.
        int numInitialSectors = 0;
        if (!SimpleCulling)
        {
            if (cullingMember && cullingMember.enabled)
            {
                initialSectors.Clear();
                initialSectors.AddRange(cullingMember.Sectors);
            }
            else
            {
                SECTR_Sector.GetContaining(ref initialSectors, new Bounds(cameraPos, new Vector3(maxNearClipDistance, maxNearClipDistance, maxNearClipDistance)));
            }

            // Simple culling is much more efficient when in a terrain grid, as the basic
            // graph algorithm has a lot of excessive traversal for very little gain.
            // As a semi-hack, auto-enable simple culling if the camera starts in part of a terrain grid.
            // A better approach would be to formally group sets of terrain grids and group them properly,
            // but that requires much smarter infrastructure.
            // Since most scenes have only one or two initial sectors, this added cost should be small.
            numInitialSectors = initialSectors.Count;
            for (int sectorIndex = 0; sectorIndex < numInitialSectors; ++sectorIndex)
            {
                SECTR_Sector sector = initialSectors[sectorIndex];
                if (sector.IsConnectedTerrain)
                {
                    SimpleCulling = true;
                    break;
                }
            }
        }
        if (SimpleCulling)
        {
            initialSectors.Clear();
            initialSectors.AddRange(SECTR_Sector.All);
            numInitialSectors = initialSectors.Count;
        }
       
        if (cullingCamera.enabled)// && enabled && numInitialSectors > 0)
        {
            Profiler.BeginSample("Setup");
#if THREAD_CULLING
            int numWorkers = workerThreads.Count;
#endif

#if UNITY_EDITOR
            // Hide the in-editor wireframe representation of the portals
            // during culling for easier visualization of which portals are visible.
            int numPortals = SECTR_Portal.All.Count;
            for (int portalIndex = 0; portalIndex < numPortals; ++portalIndex)
            {
                SECTR_Portal portal = SECTR_Portal.All[portalIndex];
                if (Selection.activeObject && Selection.activeObject == cullingCamera.gameObject && fullCulling)
                {
                    portal.Hidden = true;
                }
            }
#endif

#if UNITY_EDITOR
            if (fullCulling)
#endif
            {
                if (!MultiCameraCulling)
                {
                    if (!runOnce)
                    {
                        _HideAllMembers();
                        runOnce = true;
                    }
                    else
                    {
                        _ApplyCulling(false);
                    }
                }
                else
                {
                    _HideAllMembers();
                }
            }

            // Precompute some terms for each Member that has a shadow casting
            // light. This precomputation is necessary because it accesses Unity
            // data which is now allowed in the threaded rendering path.
            float shadowDistance = QualitySettings.shadowDistance;
            int numMembers = SECTR_Member.All.Count;
            for (int memberIndex = 0; memberIndex < numMembers; ++memberIndex)
            {
                SECTR_Member member = SECTR_Member.All[memberIndex];
                if (member.ShadowLight)
                {
                    int numMemberShadowLights = member.ShadowLights.Count;
                    for (int shadowIndex = 0; shadowIndex < numMemberShadowLights; ++shadowIndex)
                    {
                        SECTR_Member.Child shadowLight = member.ShadowLights[shadowIndex];
                        if (shadowLight.light)
                        {
                            shadowLight.shadowLightPosition = shadowLight.light.transform.position;
                            shadowLight.shadowLightRange = shadowLight.light.range;
                        }
                        member.ShadowLights[shadowIndex] = shadowLight;
                    }
                }
            }

            // We'll walk the graph in a DFS, so use a stack.
            // We accumulate shadow lights and occluders as we go along.
            // In all cases, we clear rather than allocating a new List
            // so that we don't create any more garbage than necessary. 
            nodeStack.Clear();
            shadowLights.Clear();
            visibleRenderers.Clear();
            visibleLights.Clear();
            visibleTerrains.Clear();

            // Populate the stack with all of the Sectors that contain the camera to the stack.
            // The initial Sectors are use main camera frustum.
            Plane[] initialFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
            for (int sectorIndex = 0; sectorIndex < numInitialSectors; ++sectorIndex)
            {
                SECTR_Sector sector = initialSectors[sectorIndex];
                nodeStack.Push(new VisibilityNode(this, sector, null, initialFrustumPlanes, true));
            }
            Profiler.EndSample(); // Setup

            // Walk the portal graph, culling as we go.
            // Start counter at -1 because first Sector has an implicit portal into it.
            Profiler.BeginSample("VIS Graph Walk");
            while (nodeStack.Count > 0)
            {
                VisibilityNode currentNode = nodeStack.Pop();

                if (currentNode.frustumPlanes != null)
                {
                    Profiler.BeginSample("VIS Culling");
                    cullingPlanes.Clear();
                    cullingPlanes.AddRange(currentNode.frustumPlanes);
                    int numCullingPlanes = cullingPlanes.Count;
                    // If two frustum planes intersect at a very acute angle,
                    // they may cull more pooly than necessary. To combat that,
                    // we create a synthetic border plane at the intersection.
                    for (int planeIndex = 0; planeIndex < numCullingPlanes; ++planeIndex)
                    {
                        Plane plane0 = cullingPlanes[planeIndex];
                        Plane plane1 = cullingPlanes[(planeIndex + 1) % cullingPlanes.Count];
                        float normalDot = Vector3.Dot(plane0.normal, plane1.normal);
                        // If the two planes are acute we want to add a plane, 
                        // but not if they are directly oppositional (i.e. near and far)
                        if (normalDot < -0.9f && normalDot > -0.99f)
                        {
                            // Compute the normal of the new plane
                            Vector3 vecA = plane0.normal + plane1.normal;
                            Vector3 vecB = Vector3.Cross(plane0.normal, plane1.normal);
                            Vector3 newNormal = vecA - (Vector3.Dot(vecA, vecB) * vecB);
                            newNormal.Normalize();

                            // Compute a point on the line of intersection
                            Matrix4x4 m = new Matrix4x4();
                            m.SetRow(0, new Vector4(plane0.normal.x, plane0.normal.y, plane0.normal.z, 0f));
                            m.SetRow(1, new Vector4(plane1.normal.x, plane1.normal.y, plane1.normal.z, 0f));
                            m.SetRow(2, new Vector4(vecB.x, vecB.y, vecB.z, 0f));
                            m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
                            Vector3 intersectionPos = m.inverse.MultiplyPoint3x4(new Vector3(-plane0.distance, -plane1.distance, 0f));

                            // Add the new plane, but make sure to skip it in the loop.
                            // We don't want to do any checks against our newly added plane.
                            cullingPlanes.Insert(++planeIndex, new Plane(newNormal, intersectionPos));
                        }
                    }

                    // Now test the frustum against the contents of the sector.
                    numCullingPlanes = cullingPlanes.Count;
                    int baseMask = 0;
                    for (int planeIndex = 0; planeIndex < numCullingPlanes; ++planeIndex)
                    {
                        baseMask |= 1 << planeIndex;
                    }

                    SECTR_Sector sector = currentNode.sector;

                    // Add the occluders in this sector into the traversal list
                    if (SECTR_Occluder.All.Count > 0)
                    {
                        List<SECTR_Occluder> sectorOccluders = SECTR_Occluder.GetOccludersInSector(sector);
                        if (sectorOccluders != null)
                        {
                            int numOccluders = sectorOccluders.Count;
                            for (int occluderIndex = 0; occluderIndex < numOccluders; ++occluderIndex)
                            {
                                SECTR_Occluder occluder = sectorOccluders[occluderIndex];
                                int outMask;
                                if (occluder.HullMesh && !activeOccluders.ContainsKey(occluder))
                                {
                                    Matrix4x4 localToWorld = occluder.GetCullingMatrix(cameraPos);
                                    Vector3[] srcVerts = occluder.VertsCW;
                                    // Using the reverse normal here to save time in the plane construction below.
                                    Vector3 occluderReverseNormal = localToWorld.MultiplyVector(-occluder.MeshNormal).normalized;
                                    if (srcVerts != null && !SECTR_Geometry.IsPointInFrontOfPlane(cameraPos, occluder.Center, occluderReverseNormal))
                                    {
                                        // Build both an AABB for the oriented occluder and a list of verts
                                        // that can be used to compute a frustum should the AABB be visible.
                                        int numOccluderVerts = srcVerts.Length;
                                        occluderVerts.Clear();
                                        Bounds occluderBounds = new Bounds(occluder.transform.position, Vector3.zero);
                                        for (int vertIndex = 0; vertIndex < numOccluderVerts; ++vertIndex)
                                        {
                                            Vector3 vertexWS = localToWorld.MultiplyPoint3x4(srcVerts[vertIndex]);
                                            occluderBounds.Encapsulate(vertexWS);
                                            occluderVerts.Add(new ClipVertex(new Vector4(vertexWS.x, vertexWS.y, vertexWS.z, 1), 0f));
                                        }

                                        // Frustum check to avoid including Occluders that are not actually visible.
                                        if (SECTR_Geometry.FrustumIntersectsBounds(occluder.BoundingBox, cullingPlanes, baseMask, out outMask))
                                        {
                                            // Get a list of frustum planes from the pool if possible.
                                            List<Plane> occluderFrustum;
                                            if (frustumPool.Count > 0)
                                            {
                                                occluderFrustum = frustumPool.Pop();
                                                occluderFrustum.Clear();
                                            }
                                            else
                                            {
                                                occluderFrustum = new List<Plane>(numOccluderVerts + 1);
                                            }
                                            // Build the frustum and be sure to include the plane of the occluder itself.
                                            _BuildFrustumFromHull(cullingCamera, true, occluderVerts, ref occluderFrustum);
                                            occluderFrustum.Add(new Plane(occluderReverseNormal, occluder.Center));

                                            // Package everything up for use.
                                            // Note that we could save some time by store occluders with the Visiblity Node,
                                            // pushing and poping them with the traversal, which would avoid excessive accumulation.
                                            // Not sure this is really a win since occluders are used infrequently and usually in
                                            // open environments.
                                            occluderFrustums.Add(occluderFrustum);
                                            activeOccluders[occluder] = occluder;
#if UNITY_EDITOR
                                            clipOccluderData.Add(new ClipRenderData(occluderVerts, true));
#endif
                                        }
                                    }
                                }
#if UNITY_EDITOR
                                occluder.CullingCamera = cullingCamera;
#endif
                            }
                        }
                    }

#if UNITY_EDITOR
                    if (fullCulling)
#endif
                    {
#if THREAD_CULLING
                        // If we have worker threads, enqueue a work item, signal the next thread that there is work to do
                        // and increment the amount of work remaining.
                        if (numWorkers > 0)
                        {
                            lock (cullingWorkQueue)
                            {
                                cullingWorkQueue.Enqueue(new ThreadCullData(sector, this, cameraPos, cullingPlanes, occluderFrustums, baseMask, shadowDistance, SimpleCulling));
                                Monitor.Pulse(cullingWorkQueue);
                            }
                            Interlocked.Increment(ref remainingThreadWork);
                        }
                        else // Fallback to having the main CPU do the culling as if threading was not enabled.
#endif
                        {
                            _FrustumCullSector(sector, cameraPos, cullingPlanes, occluderFrustums, baseMask, shadowDistance, SimpleCulling, ref visibleRenderers, ref visibleLights, ref visibleTerrains, ref shadowLights);
                        }
                    }
                    Profiler.EndSample(); // Culling

                    // Now we'll check our frustum against the portals in the Sector.
                    // If the Sector frustum intersects any portals, we'll create a new clipped frustum
                    // and pass that along for a future iteration of the search.
                    Profiler.BeginSample("VIS Clipping");
                    int numNodePortals = SimpleCulling ? 0 : currentNode.sector.Portals.Count;
                    for (int portalIndex = 0; portalIndex < numNodePortals; ++portalIndex)
                    {
                        SECTR_Portal nextPortal = currentNode.sector.Portals[portalIndex];
                        bool passThrough = (nextPortal.Flags & SECTR_Portal.PortalFlags.PassThrough) != 0;
                        if ((nextPortal.HullMesh || passThrough) && (nextPortal.Flags & SECTR_Portal.PortalFlags.Closed) == 0)
                        {
                            // We need to know which direction we are passing through the portal,
                            // so compute that up front based on the side we're coming through.
                            bool forwardTraversal = currentNode.sector == nextPortal.FrontSector;
                            // Get the next sector based on traversal direction.
                            SECTR_Sector nextSector = forwardTraversal ? nextPortal.BackSector : nextPortal.FrontSector;

                            // Some early out checks
                            bool earlyOut = !nextSector;

                            // Bail if we're on the wrong side of the plane.
                            if (!earlyOut)
                            {
                                earlyOut = SECTR_Geometry.IsPointInFrontOfPlane(cameraPos, nextPortal.Center, nextPortal.Normal) != forwardTraversal;
                            }

                            if (!earlyOut && currentNode.portal)
                            {
                                Vector3 vecToPortal = (nextPortal.Center - currentNode.portal.Center).normalized;
                                Vector3 entryNormal = currentNode.forwardTraversal ? currentNode.portal.ReverseNormal : currentNode.portal.Normal;
                                earlyOut = Vector3.Dot(vecToPortal, entryNormal) < 0f;
                            }

                            // bail if the portal is completely occluded, which happens whenever its AABB is
                            // completely contained by an occluder frustum.
                            if (!earlyOut && !passThrough)
                            {
                                int numOccluders = occluderFrustums.Count;
                                for (int occluderIndex = 0; occluderIndex < numOccluders; ++occluderIndex)
                                {
                                    if (SECTR_Geometry.FrustumContainsBounds(nextPortal.BoundingBox, occluderFrustums[occluderIndex]))
                                    {
                                        earlyOut = true;
                                        break;
                                    }
                                }
                            }

                            // Early out means push a dummy node onto the stack.
                            // Looking at this again, it's harmless but probably could just
                            // be a call to continue without any pushing.
                            if (earlyOut)
                            {
                                continue;
                            }

                            // Time to build a new frustrum from the clipped Portal.
                            // Transform portal vertices into world space to match our frustum planes.
                            if (!passThrough)
                            {
                                // This work could be cached for static portals, which would save some
                                // small amount of time.
                                portalVertices.Clear();
                                Matrix4x4 localToWorld = nextPortal.transform.localToWorldMatrix;
                                Vector3[] srcVerts = nextPortal.VertsCW;
                                if (srcVerts != null)
                                {
                                    int numSrcVerts = srcVerts.Length;
                                    for (int vertIndex = 0; vertIndex < numSrcVerts; ++vertIndex)
                                    {
                                        Vector3 vertexWS = localToWorld.MultiplyPoint3x4(srcVerts[vertIndex]);
                                        portalVertices.Add(new ClipVertex(new Vector4(vertexWS.x, vertexWS.y, vertexWS.z, 1), 0f));
                                    }
                                }
                            }

                            // Build up the planes of the new culling frustum
                            newFrustum.Clear();
                            if ((!passThrough && !nextPortal.IsPointInHull(cameraPos, maxNearClipDistance))
#if UNITY_EDITOR
                            || (Application.isEditor && !Application.isPlaying && !passThrough)
#endif
                               )
                            {
                                // Now clip the portal by each plane in the frustum
                                int numPlanes = currentNode.frustumPlanes.Count;
                                for (int planeIndex = 0; planeIndex < numPlanes; ++planeIndex)
                                {
                                    Plane frustrumPlane = currentNode.frustumPlanes[planeIndex];
                                    // Determine which side of the plane each vert is on 
                                    Vector4 planeVec = new Vector4(frustrumPlane.normal.x, frustrumPlane.normal.y, frustrumPlane.normal.z, frustrumPlane.distance);

                                    // We can do some extra optiizations if all verts are on one side of the plane.
                                    bool allPositive = true;
                                    bool allNegative = true;
                                    for (int vertIndex = 0; vertIndex < portalVertices.Count; ++vertIndex)
                                    {
                                        Vector4 vertex = portalVertices[vertIndex].vertex;
                                        float side = Vector4.Dot(planeVec, vertex);
                                        portalVertices[vertIndex] = new ClipVertex(vertex, side);
                                        allPositive = allPositive && side > 0;
                                        allNegative = allNegative && side <= -SECTR_Geometry.kVERTEX_EPSILON;
                                    }

                                    // If all points are on the neg side, then we can reject the entire portal
                                    if (allNegative)
                                    {
                                        portalVertices.Clear();
                                        break;
                                    }
                                    // If some points are on the positive side and some on the neg side of the plane
                                    // Then we need to clip the portal geometry by the plane.
                                    else if (!allPositive)
                                    {
                                        // First, we'll loop through the shape, inserting verts anywhere
                                        // that a pair of verts straddles the plane.
                                        int numVerts = portalVertices.Count;
                                        for (int vertIndex = 0; vertIndex < numVerts; ++vertIndex)
                                        {
                                            int nextVert = (vertIndex + 1) % portalVertices.Count;
                                            float lDotV0 = portalVertices[vertIndex].side;
                                            float lDotV1 = portalVertices[nextVert].side;
                                            if ((lDotV0 > 0f && lDotV1 <= -SECTR_Geometry.kVERTEX_EPSILON) ||
                                                (lDotV1 > 0f && lDotV0 <= -SECTR_Geometry.kVERTEX_EPSILON))
                                            {
                                                Vector4 vPos0 = portalVertices[vertIndex].vertex;
                                                Vector4 vPos1 = portalVertices[nextVert].vertex;
                                                // T is the parametric position of the new vert
                                                // between the two verts that straddle the plane.
                                                float t = lDotV0 / Vector4.Dot(planeVec, vPos0 - vPos1);
                                                Vector4 newVertPos = vPos0 + (t * (vPos1 - vPos0));
                                                newVertPos.w = 1;
                                                portalVertices.Insert(vertIndex + 1, new ClipVertex(newVertPos, 0f));
                                                ++numVerts;
                                            }
                                        }

                                        // Now that all of the new verts are added, we remove any verts
                                        // that are on the wrong side of the plane.
                                        // It's this simple because the portal verts are pre-sorted CW or CCW.
                                        int portalVertIndex = 0;
                                        while (portalVertIndex < numVerts)
                                        {
                                            if (portalVertices[portalVertIndex].side < -SECTR_Geometry.kVERTEX_EPSILON)
                                            {
                                                portalVertices.RemoveAt(portalVertIndex);
                                                --numVerts;
                                            }
                                            else
                                            {
                                                ++portalVertIndex;
                                            }
                                        }
                                    }
                                }

                                // With the final clipped portal plane we need to generate a new frustum.
                                _BuildFrustumFromHull(cullingCamera, forwardTraversal, portalVertices, ref newFrustum);
                            }
                            else
                            {
                                // If the camera is very, very close to the portal, then
                                // just pass the frustum on without modification
                                newFrustum.AddRange(initialFrustumPlanes);
                            }

                            // At long last, push the next Sector/frustum onto the stack.
                            if (newFrustum.Count > 2)
                            {
                                nodeStack.Push(new VisibilityNode(this, nextSector, nextPortal, newFrustum, forwardTraversal));
#if UNITY_EDITOR
                                clipPortalData.Add(new ClipRenderData(portalVertices, forwardTraversal));
                                if (Selection.activeObject && Selection.activeObject == cullingCamera.gameObject && fullCulling)
                                {
                                    nextPortal.Hidden = false;
                                }
#endif
                            }
                        }
                    }
                    Profiler.EndSample(); // Clipping
                }
                // Return our resources back for use in later traversal steps.
                if (currentNode.frustumPlanes != null)
                {
                    currentNode.frustumPlanes.Clear();
                    frustumPool.Push(currentNode.frustumPlanes);
                }
            }

#if THREAD_CULLING
            // Here we join all worker threads as all culling must be stopped before we proceed.
            // Rather than just spinning, the main thread will help take and process tasks from
            // the work queue, just like a worker thread.
            if (numWorkers > 0)
            {
                Profiler.BeginSample("VIS Cull Join");
                while (remainingThreadWork > 0)
                {
                    while (cullingWorkQueue.Count > 0)
                    {
                        ThreadCullData cullData = new ThreadCullData();
                        lock (cullingWorkQueue)
                        {
                            if (cullingWorkQueue.Count > 0)
                            {
                                cullData = cullingWorkQueue.Dequeue();
                            }
                        }

                        if (cullData.cullingMode == ThreadCullData.CullingModes.Graph)
                        {
                            _FrustumCullSectorThread(cullData);
                            Interlocked.Decrement(ref remainingThreadWork);
                        }
                    }
                }
                remainingThreadWork = 0;
                Profiler.EndSample(); // Cull Join
            }
#endif

            Profiler.EndSample(); // Graph Walk

            // After we've culled the primary scene, we do a post pass checking for any objects
            // that overlap any of the shadow casting point or spot lights in the scene. Because
            // the shadows cast by these objects might be visible even if the actual object is not,
            // we need to treat the actual object as if it were visible.
            Profiler.BeginSample("VIS Shadows");
            int numShadowLights = shadowLights.Count;
            if (numShadowLights > 0 && CullShadows
#if UNITY_EDITOR
               && fullCulling
#endif
               )
            {
                // Start by computing a list of shadow casters for each Sector. This is basically a poor-
                // mans spatial partitioning, trying to ensure that there is a minimum of repeated work
                // during the detailed culling.
                Profiler.BeginSample("VIS Shadow Setup");
                shadowSectorTable.Clear();
                Dictionary<SECTR_Member.Child, int>.Enumerator shadowLightEnum = shadowLights.GetEnumerator();
                while (shadowLightEnum.MoveNext())
                {
                    SECTR_Member.Child shadowLight = shadowLightEnum.Current.Key;
                    List<SECTR_Sector> affectedSectors;

                    // Shadow lights that are sectors simply add themselves.
                    if (shadowLight.member && shadowLight.member.IsSector)
                    {
                        shadowSectors.Clear();
                        shadowSectors.Add((SECTR_Sector)shadowLight.member);
                        affectedSectors = shadowSectors;
                    }
                    // Shadow lights that are members can add their current list of Sectors.
                    else if (shadowLight.member && shadowLight.member.Sectors.Count > 0)
                    {
                        affectedSectors = shadowLight.member.Sectors;
                    }
                    // Otherwise compute Sector overlap manually.
                    else
                    {
                        SECTR_Sector.GetContaining(ref shadowSectors, shadowLight.lightBounds);
                        affectedSectors = shadowSectors;
                    }

                    // Finally build up the per-sector list.
                    int numSectors = affectedSectors.Count;
                    for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
                    {
                        SECTR_Sector sector = affectedSectors[sectorIndex];
                        List<SECTR_Member.Child> sectorShadowLights;
                        if (!shadowSectorTable.TryGetValue(sector, out sectorShadowLights))
                        {
                            sectorShadowLights = shadowLightPool.Count > 0 ? shadowLightPool.Pop() : new List<SECTR_Member.Child>(16);
                            shadowSectorTable[sector] = sectorShadowLights;
                        }
                        sectorShadowLights.Add(shadowLight);
                    }
                }
                Profiler.EndSample(); // Shadow Setup

                // Now do the actuall post-pass, one sector at a time.
                Profiler.BeginSample("VIS Shadow Cull");
                Dictionary<SECTR_Sector, List<SECTR_Member.Child>>.Enumerator shadowEnum = shadowSectorTable.GetEnumerator();
                while (shadowEnum.MoveNext())
                {
                    SECTR_Sector sector = shadowEnum.Current.Key;
                    List<SECTR_Member.Child> sectorShadowLights = shadowEnum.Current.Value;
#if THREAD_CULLING
                    // If we have worker threads, enqueue a unit of work for them to do,
                    // just like in main culling.
                    if (numWorkers > 0)
                    {
                        lock (cullingWorkQueue)
                        {
                            cullingWorkQueue.Enqueue(new ThreadCullData(sector, cameraPos, sectorShadowLights));
                            Monitor.Pulse(cullingWorkQueue);
                        }
                        Interlocked.Increment(ref remainingThreadWork);
                    }
                    // Otherwise do shadow cull on the main thread as if threaded culling were disabled.
                    else
#endif
                    {
                        _ShadowCullSector(sector, sectorShadowLights, ref visibleRenderers, ref visibleTerrains);
                    }
                }
                Profiler.EndSample(); // Shadow Cull

#if THREAD_CULLING
                // As with main culling, join here, and pull tasks off until everything is done.
                if (numWorkers > 0)
                {
                    Profiler.BeginSample("VIS Shadow Join");
                    while (remainingThreadWork > 0)
                    {
                        while (cullingWorkQueue.Count > 0)
                        {
                            ThreadCullData cullData = new ThreadCullData();
                            lock (cullingWorkQueue)
                            {
                                if (cullingWorkQueue.Count > 0)
                                {
                                    cullData = cullingWorkQueue.Dequeue();
                                }
                            }

                            if (cullData.cullingMode == ThreadCullData.CullingModes.Shadow)
                            {
                                _ShadowCullSectorThread(cullData);
                                Interlocked.Decrement(ref remainingThreadWork);
                            }
                        }
                    }
                    remainingThreadWork = 0;
                    Profiler.EndSample(); // Shadow Join
                }
#endif

                // Return shadow children to the pool
                shadowEnum = shadowSectorTable.GetEnumerator();
                while (shadowEnum.MoveNext())
                {
                    List<SECTR_Member.Child> sectorShadowLights = shadowEnum.Current.Value;
                    sectorShadowLights.Clear();
                    shadowLightPool.Push(sectorShadowLights);
                }
            }
            Profiler.EndSample(); // Shadows

            // Actually hide everything that needs to be hidden.
            Profiler.BeginSample("VIS Apply");
#if UNITY_EDITOR
            if (fullCulling)
#endif
            {
                _ApplyCulling(true);
            }

            // Return occluders frustums to their pool.
            int numOccluderFrustums = occluderFrustums.Count;
            for (int occluderIndex = 0; occluderIndex < numOccluderFrustums; ++occluderIndex)
            {
                occluderFrustums[occluderIndex].Clear();
                frustumPool.Push(occluderFrustums[occluderIndex]);
            }
            occluderFrustums.Clear();
            activeOccluders.Clear();

            Profiler.EndSample(); // Apply
        }
    }

    void OnPostRender()
    {
        // Reveal all objects hidden during PreCull.
        // This doesn't completely work due to Unity bugs, but maybe
        // they will be fixed someday.
        if (MultiCameraCulling)
        {
            _UndoCulling();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (SceneView.lastActiveSceneView)
        {
            Camera sceneCamera = SceneView.lastActiveSceneView.camera;
            if (sceneCamera)
            {
                SECTR_CullingCamera sceneCamCuller = sceneCamera.GetComponent<SECTR_CullingCamera>();
                if (Selection.activeObject == gameObject && enabled && CullInEditor)
                {
                    if (sceneCamCuller == null)
                    {
                        sceneCamCuller = sceneCamera.gameObject.AddComponent<SECTR_CullingCamera>();
                    }
                    sceneCamCuller.cullingProxy = myCamera;
                    sceneCamCuller.CullShadows = CullShadows;
                    if (sceneCamCuller.MultiCameraCulling != MultiCameraCulling)
                    {
                        sceneCamCuller.runOnce = false;
                    }
                    sceneCamCuller.MultiCameraCulling = MultiCameraCulling;
                    sceneCamCuller.enabled = true;
                }
                else if (sceneCamCuller != null && sceneCamCuller.cullingProxy == myCamera)
                {
                    sceneCamCuller.cullingProxy = null;
                    sceneCamCuller.enabled = false;

                    int numPortals = SECTR_Portal.All.Count;
                    for (int portalIndex = 0; portalIndex < numPortals; ++portalIndex)
                    {
                        SECTR_Portal portal = SECTR_Portal.All[portalIndex];
                        portal.Hidden = false;
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (enabled)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 origin = cullingProxy ? cullingProxy.transform.position : transform.position;

            // Draw wireframe of frustum propagated through portals.
            Gizmos.color = ClippedPortalColor;
            int numClippedPortals = clipPortalData.Count;
            for (int portalIndex = 0; portalIndex < numClippedPortals; ++portalIndex)
            {
                ClipRenderData renderData = clipPortalData[portalIndex];
                List<ClipVertex> _clippedVerts = renderData.clippedPortalVerts;
                int numClippedVerts = _clippedVerts.Count;
                for (int vertIndex = 0; vertIndex < numClippedVerts; ++vertIndex)
                {
                    Vector3 vert = _clippedVerts[vertIndex].vertex;
                    int nextIndex = (vertIndex + 1) % numClippedVerts;
                    Vector3 nextVert = _clippedVerts[nextIndex].vertex;
                    Gizmos.DrawLine(vert, nextVert);

                    Vector3 projectedVert = vert + ((vert - origin).normalized * GizmoDistance);
                    Vector3 projectedNextVert = nextVert + ((nextVert - origin).normalized * GizmoDistance);

                    Gizmos.DrawLine(vert, projectedVert);
                    Gizmos.DrawLine(nextVert, projectedNextVert);
                    Gizmos.DrawLine(projectedVert, projectedNextVert);
                }
            }

            // Draw wireframe of frustums extruded from occluders.
            Gizmos.color = ClippedOccluderColor;
            int numClippedOccluders = clipOccluderData.Count;
            for (int occluderIndex = 0; occluderIndex < numClippedOccluders; ++occluderIndex)
            {
                ClipRenderData renderData = clipOccluderData[occluderIndex];
                List<ClipVertex> _clippedVerts = renderData.clippedPortalVerts;
                int numClippedVerts = _clippedVerts.Count;
                for (int vertIndex = 0; vertIndex < numClippedVerts; ++vertIndex)
                {
                    Vector3 vert = _clippedVerts[vertIndex].vertex;
                    int nextIndex = (vertIndex + 1) % numClippedVerts;
                    Vector3 nextVert = _clippedVerts[nextIndex].vertex;
                    Gizmos.DrawLine(vert, nextVert);

                    Vector3 projectedVert = vert + ((vert - origin).normalized * GizmoDistance);
                    Vector3 projectedNextVert = nextVert + ((nextVert - origin).normalized * GizmoDistance);

                    Gizmos.DrawLine(vert, projectedVert);
                    Gizmos.DrawLine(nextVert, projectedNextVert);
                    Gizmos.DrawLine(projectedVert, projectedNextVert);
                }
            }

            // Draw debug mesh of propagated frustum.
            if (GizmoMaterial && debugFrustumMesh)
            {
                _BuildFrustumMesh(ref debugFrustumMesh, origin, GizmoDistance);
                Gizmos.color = Color.white;
                GizmoMaterial.SetPass(0);
                Graphics.DrawMeshNow(debugFrustumMesh, Matrix4x4.identity);
            }
        }
    }
#endif
#endregion

#region Private Methods
#if THREAD_CULLING
    // The worker thread main routine.
    private void _CullingWorker()
    {
        // Culling worker goes forever until aborted in OnDisable.
        while (true)
        {
            // Because of the lock/montior lock below, always allocate something.
            // The defautl is a "do nothing" work item.
            ThreadCullData cullData = new ThreadCullData();

            // Wait until we're signaled. Because of the Monitor.Wait below,
            // this releases the lock during waiting.
            lock (cullingWorkQueue)
            {
                while (cullingWorkQueue.Count == 0)
                {
                    Monitor.Wait(cullingWorkQueue);
                }

                // Once signaled, get something from the queue.
                cullData = cullingWorkQueue.Dequeue();
            }

            // Actuall do work.
            switch (cullData.cullingMode)
            {
                case ThreadCullData.CullingModes.Graph:
                    _FrustumCullSectorThread(cullData);
                    break;
                case ThreadCullData.CullingModes.Shadow:
                    _ShadowCullSectorThread(cullData);
                    break;
                case ThreadCullData.CullingModes.None:
                default:
                    break;
            }

            // Once finished, decrement the remaining work.
            if (cullData.cullingMode == ThreadCullData.CullingModes.Graph ||
               cullData.cullingMode == ThreadCullData.CullingModes.Shadow)
            {
                Interlocked.Decrement(ref remainingThreadWork);
            }
        }
    }

    // Frustum culling setup for worker threads.
    private void _FrustumCullSectorThread(ThreadCullData cullData)
    {
        // Worker threads need scratch space.
        Dictionary<int, SECTR_Member.Child> localVisibleRenderers = null;
        Dictionary<int, SECTR_Member.Child> localVisibleLights = null;
        Dictionary<int, SECTR_Member.Child> localVisibleTerrains = null;
        Dictionary<SECTR_Member.Child, int> localShadowLights = null;

        // Get scratch space from pools if at all possible.
        lock (threadVisibleListPool)
        {
            localVisibleRenderers = threadVisibleListPool.Count > 0 ? threadVisibleListPool.Pop() : new Dictionary<int, SECTR_Member.Child>(32);
            localVisibleLights = threadVisibleListPool.Count > 0 ? threadVisibleListPool.Pop() : new Dictionary<int, SECTR_Member.Child>(32);
            localVisibleTerrains = threadVisibleListPool.Count > 0 ? threadVisibleListPool.Pop() : new Dictionary<int, SECTR_Member.Child>(32);
        }

        lock (threadShadowLightPool)
        {
            localShadowLights = threadShadowLightPool.Count > 0 ? threadShadowLightPool.Pop() : new Dictionary<SECTR_Member.Child, int>(32);
        }

        // Perform culling.
        _FrustumCullSector(cullData.sector, cullData.cameraPos, cullData.cullingPlanes, cullData.occluderFrustums, cullData.baseMask, cullData.shadowDistance, cullData.cullingSimpleCulling, ref localVisibleRenderers, ref localVisibleLights, ref localVisibleTerrains, ref localShadowLights);

        // Merge results into master lists.
        lock (visibleRenderers)
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = localVisibleRenderers.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                visibleRenderers[visibleEnum.Current.Key] = visibleEnum.Current.Value;
            }
        }
        lock (visibleLights)
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = localVisibleLights.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                visibleLights[visibleEnum.Current.Key] = visibleEnum.Current.Value;
            }
        }
        lock (visibleTerrains)
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = localVisibleTerrains.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                visibleTerrains[visibleEnum.Current.Key] = visibleEnum.Current.Value;
            }
        }
        lock (shadowLights)
        {
            Dictionary<SECTR_Member.Child, int>.Enumerator shadowEnum = localShadowLights.GetEnumerator();
            while (shadowEnum.MoveNext())
            {
                shadowLights[shadowEnum.Current.Key] = 0;
            }
        }

        // Return the scratch space to the pool
        lock (threadVisibleListPool)
        {
            localVisibleRenderers.Clear();
            threadVisibleListPool.Push(localVisibleRenderers);
            localVisibleLights.Clear();
            threadVisibleListPool.Push(localVisibleLights);
            localVisibleTerrains.Clear();
            threadVisibleListPool.Push(localVisibleTerrains);
        }

        lock (threadShadowLightPool)
        {
            localShadowLights.Clear();
            threadShadowLightPool.Push(localShadowLights);
        }

        // Return the lists allocated in the ThreadCullData constructor to the pools.
        lock (threadFrustumPool)
        {
            cullData.cullingPlanes.Clear();
            threadFrustumPool.Push(cullData.cullingPlanes);
            int numOccluders = cullData.occluderFrustums.Count;
            for (int occluderIndex = 0; occluderIndex < numOccluders; ++occluderIndex)
            {
                cullData.occluderFrustums[occluderIndex].Clear();
                threadFrustumPool.Push(cullData.occluderFrustums[occluderIndex]);
            }
        }
        lock (threadOccluderPool)
        {
            cullData.occluderFrustums.Clear();
            threadOccluderPool.Push(cullData.occluderFrustums);
        }
    }

    // Thread version of shadow post-pas culling.
    private void _ShadowCullSectorThread(ThreadCullData cullData)
    {
        // Worker threads need scratch space.
        Dictionary<int, SECTR_Member.Child> localVisibleRenderers = null;
        Dictionary<int, SECTR_Member.Child> localVisibleTerrains = null;

        // Grab scratch space form pools if at all possible.
        lock (threadVisibleListPool)
        {
            localVisibleRenderers = threadVisibleListPool.Count > 0 ? threadVisibleListPool.Pop() : new Dictionary<int, SECTR_Member.Child>(32);
            localVisibleTerrains = threadVisibleListPool.Count > 0 ? threadVisibleListPool.Pop() : new Dictionary<int, SECTR_Member.Child>(32);
        }

        // Perform shadow culling.
        _ShadowCullSector(cullData.sector, cullData.sectorShadowLights, ref localVisibleRenderers, ref localVisibleTerrains);

        // Populate master lists with results.
        lock (visibleRenderers)
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = localVisibleRenderers.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                visibleRenderers[visibleEnum.Current.Key] = visibleEnum.Current.Value;
            }
        }
        lock (visibleTerrains)
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = localVisibleTerrains.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                visibleTerrains[visibleEnum.Current.Key] = visibleEnum.Current.Value;
            }
        }

        // Return scratch space to the pool.
        lock (threadVisibleListPool)
        {
            localVisibleRenderers.Clear();
            threadVisibleListPool.Push(localVisibleRenderers);
            localVisibleTerrains.Clear();
            threadVisibleListPool.Push(localVisibleTerrains);
        }
    }
#endif

    // Performs frustum culling of a Sector, writing results into ref Lists.
    private static void _FrustumCullSector(SECTR_Sector sector,
                                   Vector3 cameraPos,
                                   List<Plane> cullingPlanes,
                                   List<List<Plane>> occluderFrustums,
                                   int baseMask,
                                   float shadowDistance,
                                   bool forceGroupCull,
                                   ref Dictionary<int, SECTR_Member.Child> visibleRenderers,
                                   ref Dictionary<int, SECTR_Member.Child> visibleLights,
                                   ref Dictionary<int, SECTR_Member.Child> visibleTerrains,
                                   ref Dictionary<SECTR_Member.Child, int> shadowLights)
    {
        // Cull the Sector itself.
        _FrustumCull(sector, cameraPos, cullingPlanes, occluderFrustums, baseMask, shadowDistance, forceGroupCull, ref visibleRenderers, ref visibleLights, ref visibleTerrains, ref shadowLights);
        // Cull individual members that overlap the Sector
        int numDynamicChildren = sector.Members.Count;
        for (int dynamicChildIndex = 0; dynamicChildIndex < numDynamicChildren; ++dynamicChildIndex)
        {
            SECTR_Member member = sector.Members[dynamicChildIndex];
            if (member.HasRenderBounds || member.HasLightBounds)
            {
                _FrustumCull(member, cameraPos, cullingPlanes, occluderFrustums, baseMask, shadowDistance, forceGroupCull, ref visibleRenderers, ref visibleLights, ref visibleTerrains, ref shadowLights);
            }
        }
    }

    // Performs frustum culling of a Sector/Member, writing results into ref Lists.
    private static void _FrustumCull(SECTR_Member member,
                                    Vector3 cameraPos,
                                    List<Plane> frustumPlanes,
                                    List<List<Plane>> occluders,
                                    int baseMask,
                                    float shadowDistance,
                                    bool forceGroupCull,
                                    ref Dictionary<int, SECTR_Member.Child> visibleRenderers,
                                    ref Dictionary<int, SECTR_Member.Child> visibleLights,
                                    ref Dictionary<int, SECTR_Member.Child> visibleTerrains,
                                    ref Dictionary<SECTR_Member.Child, int> shadowLights
                                    )
    {

        int baseRenderMask = baseMask;
        int baseLightMask = baseMask;

        int parentRenderMask = 0;
        int parentLightMask = 0;

        bool cullEachChild = member.CullEachChild && !forceGroupCull;

        // First, compute culling against gross bounds, for possible use as an early out.
        // In the culling below, these results will be used in lieu of per-child culling
        // for any SectorCullers with CullIndividualChildren set to false.
        bool memberRenderVisible = member.HasRenderBounds && SECTR_Geometry.FrustumIntersectsBounds(member.RenderBounds, frustumPlanes, baseRenderMask, out parentRenderMask);
        bool memberLightVisible = member.HasLightBounds && SECTR_Geometry.FrustumIntersectsBounds(member.LightBounds, frustumPlanes, baseLightMask, out parentLightMask);

        // Check the global bounds against any and all occluders
        int numOccluders = occluders.Count;
        for (int occluderIndex = 0; occluderIndex < numOccluders && (memberRenderVisible || memberLightVisible); ++occluderIndex)
        {
            List<Plane> occluder = occluders[occluderIndex];
            if (memberRenderVisible)
            {
                memberRenderVisible = !SECTR_Geometry.FrustumContainsBounds(member.RenderBounds, occluder);
            }
            if (memberLightVisible)
            {
                memberLightVisible = !SECTR_Geometry.FrustumContainsBounds(member.LightBounds, occluder);
            }
        }

        // If aggregate bounds are visible, process children
        if (memberRenderVisible)
        {
            int numRenderers = member.Renderers.Count;
            for (int rendererIndex = 0; rendererIndex < numRenderers; ++rendererIndex)
            {
                SECTR_Member.Child child = member.Renderers[rendererIndex];
                if (child.renderHash != 0 &&
                   !visibleRenderers.ContainsKey(child.renderHash))
                {
                    if (!cullEachChild || _IsVisible(child.rendererBounds, frustumPlanes, parentRenderMask, occluders))
                    {
                        visibleRenderers.Add(child.renderHash, child);
                    }
                }
            }

            int numTerrains = member.Terrains.Count;
            for (int terrainIndex = 0; terrainIndex < numTerrains; ++terrainIndex)
            {
                SECTR_Member.Child child = member.Terrains[terrainIndex];
                if (child.terrainHash != 0 &&
                   !visibleTerrains.ContainsKey(child.terrainHash))
                {
                    if (!cullEachChild || _IsVisible(child.terrainBounds, frustumPlanes, parentRenderMask, occluders))
                    {
                        visibleTerrains.Add(child.terrainHash, child);
                    }
                }
            }

        }

        if (memberLightVisible)
        {
            int numLights = member.Lights.Count;
            for (int lightIndex = 0; lightIndex < numLights; ++lightIndex)
            {
                SECTR_Member.Child child = member.Lights[lightIndex];
                if (child.lightHash != 0 &&
                   !visibleLights.ContainsKey(child.lightHash))
                {
                    if (!cullEachChild || _IsVisible(child.lightBounds, frustumPlanes, parentRenderMask, occluders))
                    {
                        visibleLights.Add(child.lightHash, child);
                        if (child.shadowLight &&
                           !shadowLights.ContainsKey(child) &&
                           Vector3.Distance(cameraPos, child.shadowLightPosition) - child.shadowLightRange <= shadowDistance)
                        {
                            shadowLights.Add(child, 0);
                        }
                    }
                }
            }
        }
    }

    // Shadow post pass for a Sector, write results into ref Lists.
    private static void _ShadowCullSector(SECTR_Sector sector, List<SECTR_Member.Child> sectorShadowLights, ref Dictionary<int, SECTR_Member.Child> visibleRenderers, ref Dictionary<int, SECTR_Member.Child> visibleTerrains)
    {
        // Post-cull Sector if it's a shadow caster.
        if (sector.ShadowCaster)
        {
            _ShadowCull(sector, sectorShadowLights, ref visibleRenderers, ref visibleTerrains);
        }

        // Cull any members that overlap the Sector.
        int numDynamicChildren = sector.Members.Count;
        for (int dynamicChildIndex = 0; dynamicChildIndex < numDynamicChildren; ++dynamicChildIndex)
        {
            SECTR_Member member = sector.Members[dynamicChildIndex];
            if (member.ShadowCaster)
            {
                _ShadowCull(member, sectorShadowLights, ref visibleRenderers, ref visibleTerrains);
            }
        }
    }

    // Shadow cull an individual Sector/Member.
    private static void _ShadowCull(SECTR_Member member, List<SECTR_Member.Child> shadowLights, ref Dictionary<int, SECTR_Member.Child> visibleRenderers, ref Dictionary<int, SECTR_Member.Child> visibleTerrains)
    {
        int numShadowLights = shadowLights.Count;
        int numShadowCasters = member.ShadowCasters.Count;

        // Unlike basic culling, the optimal flow of shadow testing depends
        // on the value of CullIndividualChildren. Knowing this value
        // allows smarter early outs and other computational savings.
        if (member.CullEachChild)
        {
            // If we're culling individual children, loop over renderers and then
            // over light sources. We can bail as soon as we find any light that overlaps,
            // and we can skip the global bounds as they don't help us.
            for (int shadowCasterIndex = 0; shadowCasterIndex < numShadowCasters; ++shadowCasterIndex)
            {
                SECTR_Member.Child shadowCasterChild = member.ShadowCasters[shadowCasterIndex];
                if (shadowCasterChild.renderHash != 0 &&
                   !visibleRenderers.ContainsKey(shadowCasterChild.renderHash))
                {
                    for (int shadowLightIndex = 0; shadowLightIndex < numShadowLights; ++shadowLightIndex)
                    {
                        SECTR_Member.Child shadowLightChild = shadowLights[shadowLightIndex];
                        if (((shadowLightChild.shadowCullingMask & 1 << shadowCasterChild.layer) != 0))
                        {
                            if ((shadowLightChild.shadowLightType == LightType.Spot && shadowCasterChild.rendererBounds.Intersects(shadowLightChild.lightBounds)) ||
                               (shadowLightChild.shadowLightType == LightType.Point && SECTR_Geometry.BoundsIntersectsSphere(shadowCasterChild.rendererBounds, shadowLightChild.shadowLightPosition, shadowLightChild.shadowLightRange)))
                            {
                                visibleRenderers.Add(shadowCasterChild.renderHash, shadowCasterChild);
                                break;
                            }
                        }
                    }
                }
                if (shadowCasterChild.terrainHash != 0 &&
                   !visibleTerrains.ContainsKey(shadowCasterChild.terrainHash))
                {
                    for (int shadowLightIndex = 0; shadowLightIndex < numShadowLights; ++shadowLightIndex)
                    {
                        SECTR_Member.Child shadowLightChild = shadowLights[shadowLightIndex];
                        if (((shadowLightChild.shadowCullingMask & 1 << shadowCasterChild.layer) != 0))
                        {
                            if ((shadowLightChild.shadowLightType == LightType.Spot && shadowCasterChild.terrainBounds.Intersects(shadowLightChild.lightBounds)) ||
                               (shadowLightChild.shadowLightType == LightType.Point && SECTR_Geometry.BoundsIntersectsSphere(shadowCasterChild.terrainBounds, shadowLightChild.shadowLightPosition, shadowLightChild.shadowLightRange)))
                            {
                                visibleTerrains.Add(shadowCasterChild.terrainHash, shadowCasterChild);
                                break;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // If we're not culling individual children, loop over shadow casting lights
            // and then over individual members. However, we only have to do one
            // bounds test per-light, rather than per-child.
            for (int shadowLightIndex = 0; shadowLightIndex < numShadowLights; ++shadowLightIndex)
            {
                SECTR_Member.Child shadowLightChild = shadowLights[shadowLightIndex];

                // A child is affected by shadows if:
                // * The shadow bounds overlaps the member's render bounds (which includes terrain and renderers).
                // * They have a renderer or terrain that's enabled and not already visible.
                // * They cast dynamic shadows (computed in Child constructur)
                // * Their mask matches the light's mask.
                bool memberShadowed = shadowLightChild.shadowLightType == LightType.Spot ? member.RenderBounds.Intersects(shadowLightChild.lightBounds) :
                    SECTR_Geometry.BoundsIntersectsSphere(member.RenderBounds, shadowLightChild.shadowLightPosition, shadowLightChild.shadowLightRange);
                if (memberShadowed)
                {
                    int cullingMask = shadowLightChild.shadowCullingMask;
                    for (int shadowCasterIndex = 0; shadowCasterIndex < numShadowCasters; ++shadowCasterIndex)
                    {
                        SECTR_Member.Child shadowCasterChild = member.ShadowCasters[shadowCasterIndex];
                        if (shadowCasterChild.renderHash != 0 && shadowCasterChild.terrainHash != 0)
                        {
                            // If a Child has both a renderer and a terrain, they share a game object, so use either.
                            if ((cullingMask & 1 << shadowCasterChild.layer) != 0)
                            {
                                if (!visibleRenderers.ContainsKey(shadowCasterChild.renderHash))
                                {
                                    visibleRenderers.Add(shadowCasterChild.renderHash, shadowCasterChild);
                                }
                                if (!visibleTerrains.ContainsKey(shadowCasterChild.terrainHash))
                                {
                                    visibleTerrains.Add(shadowCasterChild.terrainHash, shadowCasterChild);
                                }
                            }
                        }
                        else if (shadowCasterChild.renderHash != 0 && !visibleRenderers.ContainsKey(shadowCasterChild.renderHash) &&
                                (cullingMask & 1 << shadowCasterChild.layer) != 0)
                        {
                            visibleRenderers.Add(shadowCasterChild.renderHash, shadowCasterChild);
                        }
                        else if (shadowCasterChild.terrainHash != 0 && !visibleTerrains.ContainsKey(shadowCasterChild.terrainHash) &&
                                (cullingMask & 1 << shadowCasterChild.layer) != 0)
                        {
                            visibleTerrains.Add(shadowCasterChild.terrainHash, shadowCasterChild);
                        }
                    }
                }
            }
        }
    }

    // Determines if an AABB is visible using a frustum and set of occluders.
    private static bool _IsVisible(Bounds childBounds, List<Plane> frustumPlanes, int parentMask, List<List<Plane>> occluders)
    {
        int childOutMask;
        if (SECTR_Geometry.FrustumIntersectsBounds(childBounds, frustumPlanes, parentMask, out childOutMask))
        {
            int numOccluders = occluders.Count;
            for (int occluderIndex = 0; occluderIndex < numOccluders; ++occluderIndex)
            {
                if (SECTR_Geometry.FrustumContainsBounds(childBounds, occluders[occluderIndex]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    // Mark all members as hidden as they will be un-hidden based on visibility.
    private void _HideAllMembers()
    {
        visibleClothRenderers.Clear();
        int numMembers = SECTR_Member.All.Count;
        for (int memberIndex = 0; memberIndex < numMembers; ++memberIndex)
        {
            // Loop over all of the children of the sibling SectorMember.
            // Anything that's not in a visible list is, by definition, invisible.
            SECTR_Member member = SECTR_Member.All[memberIndex];
            int numRenderers = member.Renderers.Count;
            for (int rendererIndex = 0; rendererIndex < numRenderers; ++rendererIndex)
            {
                SECTR_Member.Child child = member.Renderers[rendererIndex];
                child.renderCulled = true;
                if (child.renderer)
                {
                    //Special treatment for cloth rendering, we must not hide those early, 
                    //else it will disturb the cloth simulation
                    if (child.renderer.GetType() == typeof(SkinnedMeshRenderer) &&
                        child.gameObject.GetComponent<Cloth>() != null &&
                        child.renderer.enabled)
                    {
                        visibleClothRenderers.Add(child);
                    }
                    else
                    {
                        child.renderer.enabled = false;
                        hiddenRenderers[child.renderHash] = child;
                    }
                }
            }

            int numLights = member.Lights.Count;
            for (int lightIndex = 0; lightIndex < numLights; ++lightIndex)
            {
                SECTR_Member.Child child = member.Lights[lightIndex];
                child.lightCulled = true;
                if (child.light)
                {
                    child.light.enabled = false;
                }
                hiddenLights[child.lightHash] = child;
            }

            int numTerrains = member.Terrains.Count;
            for (int terrainIndex = 0; terrainIndex < numTerrains; ++terrainIndex)
            {
                SECTR_Member.Child child = member.Terrains[terrainIndex];
                child.terrainCulled = true;
                if (child.terrain)
                {
                    child.terrain.drawHeightmap = false;
                    child.terrain.drawTreesAndFoliage = false;
                }
                hiddenTerrains[child.terrainHash] = child;
            }
        }
    }

    // Use the visibility information computed in the previous culling steps to actually
    // hide (i.e. enabled = false) renderers and lights affected by this Culler.
    private void _ApplyCulling(bool visible)
    {
        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = visibleRenderers.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                SECTR_Member.Child child = visibleEnum.Current.Value;
                if (child.renderer)
                {
                    child.renderer.enabled = visible;
                }
                child.renderCulled = !visible;
                if (visible)
                {
                    hiddenRenderers.Remove(visibleEnum.Current.Key);
                }
                else
                {
                    hiddenRenderers[visibleEnum.Current.Key] = child;
                }
            }
            if (visible)
            {
                renderersCulled = hiddenRenderers.Count;
            }
        }

        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = visibleLights.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                SECTR_Member.Child child = visibleEnum.Current.Value;
                if (child.light)
                {
                    child.light.enabled = visible;
                }
                child.lightCulled = !visible;
                if (visible)
                {
                    hiddenLights.Remove(visibleEnum.Current.Key);
                }
                else
                {
                    hiddenLights[visibleEnum.Current.Key] = child;
                }
            }
            if (visible)
            {
                lightsCulled = hiddenLights.Count;
            }
        }

        {
            Dictionary<int, SECTR_Member.Child>.Enumerator visibleEnum = visibleTerrains.GetEnumerator();
            while (visibleEnum.MoveNext())
            {
                SECTR_Member.Child child = visibleEnum.Current.Value;
                if (child.terrain)
                {
                    child.terrain.drawHeightmap = visible;
                    child.terrain.drawTreesAndFoliage = visible;
                }
                child.terrainCulled = !visible;
                if (visible)
                {
                    hiddenTerrains.Remove(visibleEnum.Current.Key);
                }
                else
                {
                    hiddenTerrains[visibleEnum.Current.Key] = child;
                }
            }
            if (visible)
            {
                terrainsCulled = hiddenTerrains.Count;
            }
        }


        foreach (var child in visibleClothRenderers)
        {
            if (child.renderer)
            {
                if (!visibleRenderers.ContainsValue(child))
                {
                    child.renderer.enabled = false;
                }
            }
        }
        didCull = true;
    }

    // Reverses the effects of ApplyCulling.
    private void _UndoCulling()
    {
        if (didCull)
        {
            // Reveal anything hidden and reset book keeping datastructures.
            {
                Dictionary<int, SECTR_Member.Child>.Enumerator hiddenEnum = hiddenRenderers.GetEnumerator();
                while (hiddenEnum.MoveNext())
                {
                    SECTR_Member.Child child = hiddenEnum.Current.Value;
                    if (child.renderer)
                    {
                        child.renderer.enabled = true;
                    }
                    child.renderCulled = false;
                }
                hiddenRenderers.Clear();
            }

            {
                Dictionary<int, SECTR_Member.Child>.Enumerator hiddenEnum = hiddenLights.GetEnumerator();
                while (hiddenEnum.MoveNext())
                {
                    SECTR_Member.Child child = hiddenEnum.Current.Value;
                    if (child.light)
                    {
                        child.light.enabled = true;
                    }
                    child.lightCulled = false;
                }
                hiddenLights.Clear();
            }

            {
                Dictionary<int, SECTR_Member.Child>.Enumerator hiddenEnum = hiddenTerrains.GetEnumerator();
                while (hiddenEnum.MoveNext())
                {
                    SECTR_Member.Child child = hiddenEnum.Current.Value;
                    Terrain terrain = child.terrain;
                    if (child.terrain)
                    {
                        terrain.drawHeightmap = true;
                        terrain.drawTreesAndFoliage = true;
                    }
                    child.terrainCulled = false;
                }
                hiddenTerrains.Clear();
            }
            didCull = false;
        }
    }

    // Generates a list of planes from a list of verts.
    private void _BuildFrustumFromHull(Camera cullingCamera, bool forwardTraversal, List<ClipVertex> portalVertices, ref List<Plane> newFrustum)
    {
        // If there are not at least three verts, we can skip the portal
        // (and skip the Sector beyond) entirely.
        int numPortalVerts = portalVertices.Count;
        if (numPortalVerts > 2)
        {
            // Each edge of the portal polygon will become a new plane.
            for (int portalVertIndex = 0; portalVertIndex < numPortalVerts; ++portalVertIndex)
            {
                Vector3 vert0 = portalVertices[portalVertIndex].vertex;
                Vector3 vert1 = portalVertices[(portalVertIndex + 1) % numPortalVerts].vertex;
                Vector3 edgeVec = vert1 - vert0;
                if (Vector3.SqrMagnitude(edgeVec) > SECTR_Geometry.kVERTEX_EPSILON)
                {
                    Vector3 cameraVec = vert0 - cullingCamera.transform.position;
                    Vector3 planeVec = forwardTraversal ? Vector3.Cross(edgeVec, cameraVec) : Vector3.Cross(cameraVec, edgeVec);
                    planeVec.Normalize();
                    newFrustum.Add(new Plane(planeVec, vert0));
                }
            }
        }
    }

#if UNITY_EDITOR
    private void _BuildFrustumMesh(ref Mesh mesh, Vector3 origin, float projDist)
    {
        mesh.Clear();
        List<Vector3> verts = new List<Vector3>(32);
        List<Vector3> normals = new List<Vector3>(32);
        List<Vector2> uvs = new List<Vector2>(32);
        List<Color> vertColors = new List<Color>(32);
        List<int> tris = new List<int>(32);

        int numClippedPortals = clipPortalData.Count;
        for (int portalIndex = 0; portalIndex < numClippedPortals; ++portalIndex)
        {
            ClipRenderData renderData = clipPortalData[portalIndex];
            List<ClipVertex> _clippedVerts = renderData.clippedPortalVerts;
            int numClippedVerts = _clippedVerts.Count;
            for (int vertIndex = 0; vertIndex < numClippedVerts; ++vertIndex)
            {
                int vertIndex0 = renderData.forward ? vertIndex : numClippedVerts - 1 - vertIndex;
                Vector3 vert0 = _clippedVerts[vertIndex0].vertex;
                int vertIndex1 = renderData.forward ? (vertIndex0 + 1) % numClippedVerts : vertIndex0 - 1;
                if (vertIndex1 < 0)
                {
                    vertIndex1 = numClippedVerts - 1;
                }
                Vector3 vert1 = _clippedVerts[vertIndex1].vertex;

                Vector3 projectedVert0 = vert0 + ((vert0 - origin).normalized * projDist);
                Vector3 projectedVert1 = vert1 + ((vert1 - origin).normalized * projDist);

                int baseIndex = verts.Count;
                verts.Add(vert0);
                verts.Add(projectedVert0);
                verts.Add(vert1);
                verts.Add(projectedVert1);

                Vector3 edgeNormal = Vector3.Cross((vert1 - vert0).normalized, (projectedVert0 - vert0).normalized).normalized;

                normals.Add(edgeNormal);
                normals.Add(edgeNormal);
                normals.Add(edgeNormal);
                normals.Add(edgeNormal);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));

                vertColors.Add(new Color(1, 1, 1, 1));
                vertColors.Add(new Color(1, 1, 1, 0));
                vertColors.Add(new Color(1, 1, 1, 1));
                vertColors.Add(new Color(1, 1, 1, 0));

                tris.Add(baseIndex);
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 3);

                tris.Add(baseIndex);
                tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 2);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = vertColors.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
    }
#endif
#endregion
}
