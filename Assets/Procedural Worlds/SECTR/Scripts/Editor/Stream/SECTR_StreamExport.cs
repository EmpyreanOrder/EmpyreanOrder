// Copyright (c) 2014 Make Code Now! LLC
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using System.Linq;

/// \ingroup Stream
/// A set of static utility functions for exporting scenes and doing other stream related processing.
/// 
/// In order to stream a scene, we need to split the base scene up into multiple levels. We use levels
/// and additive addition instead of Resource Bundles because they take less memory during load and
/// do not cause assets to be duplicated on disk.
public static class SECTR_StreamExport
{
    #region Public Interface
    /// Re-adds the data from the specified Sector to the current scene. Safe to call from command line. 
    /// <param name="sector">The Sector to import.</param>
    /// <returns>Returns true if Sector was successfully imported, false otherwise.</returns>
    public static bool ImportFromChunk(SECTR_Sector sector)
    {
        if (sector == null)
        {
            Debug.LogError("Cannot import invalid Sector.");
            return false;
        }

        if (!sector.Frozen)
        {
            Debug.Log("Skipping import of unfrozen Sector");
            return true;
        }

        if (!sector.gameObject.isStatic && !SECTR_FloatingPointFix.IsActive)
        {
            Debug.Log("Skipping import of dynamic Sector " + sector.name + ".");
            return true;
        }

        SECTR_Chunk chunk = sector.GetComponent<SECTR_Chunk>();
        if (chunk)
        {
            Scene loadedScene = EditorSceneManager.OpenScene(chunk.NodeName, OpenSceneMode.Additive);
            GameObject newNode = GameObject.Find(chunk.NodeName);
            if (newNode == null)
            {
                Debug.LogError("Exported data does not match scene. Skipping import of " + sector.name + ".");
                return false;
            }

            SECTR_ChunkRef chunkRef = newNode.GetComponent<SECTR_ChunkRef>();
            if (chunkRef && chunkRef.RealSector)
            {
                newNode = chunkRef.RealSector.gameObject;
                if (chunkRef.Recentered)
                {
                    newNode.transform.parent = sector.transform;
                    newNode.transform.localPosition = Vector3.zero;
                    newNode.transform.localRotation = Quaternion.identity;
                    newNode.transform.localScale = Vector3.one;
                }
                newNode.transform.parent = null;
                DeleteObject(chunkRef.gameObject);
            }

            while (newNode.transform.childCount > 0)
            {
                newNode.transform.GetChild(0).parent = sector.transform;
            }


            // Copy terrain component specially because the generic routine doesn't work for some reason.
            Terrain terrain = newNode.GetComponent<Terrain>();
            if (terrain)
            {
                Terrain terrainClone = sector.gameObject.AddComponent<Terrain>();
                terrainClone.terrainData = terrain.terrainData;
                terrainClone.basemapDistance = terrain.basemapDistance;
#if UNITY_2019_1_OR_NEWER
                terrainClone.shadowCastingMode = terrain.shadowCastingMode;
#else
                terrainClone.castShadows = terrain.castShadows; 
#endif
                terrainClone.detailObjectDensity = terrain.detailObjectDensity;
                terrainClone.detailObjectDistance = terrain.detailObjectDistance;
                terrainClone.heightmapMaximumLOD = terrain.heightmapMaximumLOD;
                terrainClone.heightmapPixelError = terrain.heightmapPixelError;
                terrainClone.lightmapIndex = terrain.lightmapIndex;
                terrainClone.treeBillboardDistance = terrain.treeBillboardDistance;
                terrainClone.treeCrossFadeLength = terrain.treeCrossFadeLength;
                terrainClone.treeDistance = terrain.treeDistance;
                terrainClone.treeMaximumFullLODCount = terrain.treeMaximumFullLODCount;
                terrainClone.Flush();
            }

            // Destroy the placeholder Member if there is one.
            // It's theoretically possible to have multiple members, so remove them all.
            SECTR_Member[] oldMembers = newNode.GetComponents<SECTR_Member>();
            int numOldMembers = oldMembers.Length;
            for (int oldIndex = 0; oldIndex < numOldMembers; ++oldIndex)
            {
                DeleteObject(oldMembers[oldIndex]);
            }

            // Copy all remaining components over
            Component[] remainingComponents = newNode.GetComponents<Component>();
            int numRemaining = remainingComponents.Length;
            for (int componentIndex = 0; componentIndex < numRemaining; ++componentIndex)
            {
                Component component = remainingComponents[componentIndex];
                if (component != newNode.transform && component.GetType() != typeof(Terrain) && component.GetType() != typeof(SECTR_Sector) && component.GetType() != typeof(SECTR_Chunk))
                {
                    Component componentClone = sector.gameObject.AddComponent(component.GetType());
                    EditorUtility.CopySerialized(component, componentClone);
                }
            }

            // Enable a TerrainComposer node if there is one.
            MonoBehaviour terrainNeighbors = sector.GetComponent("TerrainNeighbors") as MonoBehaviour;
            if (terrainNeighbors)
            {
                terrainNeighbors.enabled = true;
            }

            DeleteObject(newNode);
            sector.Frozen = false;
            sector.ForceUpdate(true);
            chunk.enabled = false;

            //Close & remove the scene we loaded from
            EditorSceneManager.CloseScene(loadedScene, true);

            return true;
        }
        return false;
    }


    #region Old Version of Export To Chunk

    /// Exports the specific Sector into an external level file, deleting the current scene copy in the process. Safe to call from command line. 
    /// <param name="sector">The Sector to export.</param>
    /// <returns>Returns true if Sector was successfully exported, false otherwise.</returns>
    //    public static bool ExportToChunk_Old(SECTR_Sector sector)
    //    {
    //        if (string.IsNullOrEmpty(SECTR_Asset.CurrentScene()))
    //        {
    //            Debug.LogError("Scene must be saved befor export.");
    //            return false;
    //        }

    //        if (sector == null)
    //        {
    //            Debug.LogError("Cannot export null Sector.");
    //            return false;
    //        }

    //        if (!sector.gameObject.activeInHierarchy)
    //        {
    //            Debug.LogError("Cannot export inactive Sectors.");
    //            return false;
    //        }

    //        if (!sector.gameObject.isStatic && !SECTR_FloatingPointFix.IsActive)
    //        {
    //            Debug.Log("Skipping export of dynamic sector" + sector.name + ".");
    //            return true;
    //        }

    //        if (sector.Frozen)
    //        {
    //            // Already exported
    //            Debug.Log("Skipping frozen sector " + sector.name);
    //            return true;
    //        }

    //        string sceneDir;
    //        string sceneName;
    //        string exportDir = SECTR_Asset.MakeExportFolder("Chunks", false, out sceneDir, out sceneName);
    //        if (string.IsNullOrEmpty(exportDir))
    //        {
    //            Debug.LogError("Could not create Chunks folder.");
    //            return false;
    //        }

    //        // Delete the previous export, if there is one.
    //        // Prevents duplicate names piling up.
    //        SECTR_Chunk oldChunk = sector.GetComponent<SECTR_Chunk>();
    //        if (oldChunk)
    //        {
    //            AssetDatabase.DeleteAsset(oldChunk.NodeName);
    //            SECTR_VC.WaitForVC();
    //        }

    //        // Sectors are not guaranteed to be uniquely named, so always generate a unique name. 
    //        string originalSectorName = sector.name;
    //        string newAssetPath = AssetDatabase.GenerateUniqueAssetPath(exportDir + sceneName + "_" + originalSectorName + ".unity");
    //        sector.name = newAssetPath;

    //        // Make sure the current scene is saved, preserving all changes.
    //        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    //        SECTR_VC.WaitForVC();

    //        string originalScene = SECTR_Asset.CurrentScene();
    //        List<EditorBuildSettingsScene> sceneSettings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

    //        // SaveScene can cause crashes w/ version control, so we work around it with a copy.
    //        AssetDatabase.CopyAsset(originalScene, newAssetPath);
    //        SECTR_VC.WaitForVC();

    //        EditorSceneManager.OpenScene(newAssetPath, OpenSceneMode.Single);
    //        SECTR_VC.WaitForVC();

    //        sector = _FindSectorByName(newAssetPath);

    //        // Make sure to force update all members so that membership info is correct.
    //        List<SECTR_Member> allMembers = FindAllOfType<SECTR_Member>();
    //        for (int memberIndex = 0; memberIndex < allMembers.Count; ++memberIndex)
    //        {
    //            allMembers[memberIndex].ForceUpdate(true);
    //        }

    //        // Multi-sector members need to stay in the master scene.
    //        foreach (SECTR_Member member in allMembers)
    //        {
    //            if (member.Sectors.Count > 1 && member.transform.IsChildOf(sector.transform))
    //            {
    //                bool unparentMember = true;

    //                // Only affect the first member in the hierarchy below the sector
    //                Transform parent = member.transform.parent;
    //                while (parent != sector.transform)
    //                {
    //                    if (parent.GetComponent<SECTR_Member>() != null)
    //                    {
    //                        unparentMember = false;
    //                        break;
    //                    }
    //                    parent = parent.parent;
    //                }

    //                if (unparentMember)
    //                {
    //#if UNITY_2018_3_OR_NEWER
    //                    if (PrefabUtility.GetPrefabInstanceStatus(sector.gameObject) != PrefabInstanceStatus.NotAPrefab)
    //#else
    //                    if(PrefabUtility.GetPrefabType(sector.gameObject) != PrefabType.None)
    //#endif
    //                    {
    //                        Debug.LogWarning("Export is unparenting shared member " + member.name + " from prefab Sector " + sector.name + ". This will break the prefab.");
    //                    }

    //                    member.transform.parent = null;
    //                }
    //            }
    //        }

    //        // Unparent the sector from anything
    //        sector.transform.parent = null;

    //        // Any children of this sector should be exported.
    //        // The rest should be destroyed.
    //        List<Transform> allXforms = FindAllOfType<Transform>();
    //        foreach (Transform transform in allXforms)
    //        {
    //            if (transform && transform.IsChildOf(sector.transform))
    //            {
    //            }
    //            else if (transform)
    //            {
    //                //GameObject.DestroyImmediate(transform.gameObject);
    //                DeleteObject(transform.gameObject);
    //            }
    //        }

    //        GameObject dummyParent = new GameObject(newAssetPath);
    //        SECTR_ChunkRef chunkRef = dummyParent.AddComponent<SECTR_ChunkRef>();
    //        chunkRef.RealSector = sector.transform;
    //        sector.transform.parent = dummyParent.transform;

    //        // If the sector has a chunk marked for re-use, perform some special work.
    //        SECTR_Chunk originalChunk = sector.GetComponent<SECTR_Chunk>();
    //        if (originalChunk && originalChunk.ExportForReuse)
    //        {
    //            chunkRef.Recentered = true;
    //            sector.transform.localPosition = Vector3.zero;
    //            sector.transform.localRotation = Quaternion.identity;
    //            sector.transform.localScale = Vector3.one;
    //            sector.gameObject.SetActive(false);
    //        }

    //        // Rename the real chunk root with a clear name.
    //        sector.name = originalSectorName + "_Chunk";

    //        // Strip off any functional objects that will be preserved in the root scene.
    //        // Destroy the chunk first because it has dependencies on Sector.
    //        DeleteObject(originalChunk);
    //        Component[] components = sector.GetComponents<Component>();
    //        foreach (Component component in components)
    //        {
    //            if (component.GetType().IsSubclassOf(typeof(MonoBehaviour)) &&
    //               component.GetType() != typeof(Terrain) && component.GetType() != typeof(SECTR_LightmapRef))
    //            {
    //                DeleteObject(component);
    //            }
    //        }

    //        // Re-add a member that will persist all of the references and save us work post load.
    //        SECTR_Member refMember = chunkRef.RealSector.gameObject.AddComponent<SECTR_Member>();
    //        refMember.NeverJoin = true;
    //        refMember.BoundsUpdateMode = SECTR_Member.BoundsUpdateModes.Static;
    //        refMember.ForceUpdate(true);

    //        if (SECTR_FloatingPointFix.IsActive)
    //        {
    //            chunkRef.RealSector.gameObject.AddComponent<SECTR_FloatingPointFixMember>();
    //            refMember.BoundsUpdateMode = SECTR_Member.BoundsUpdateModes.Always;
    //        }

    //        // Save scene and append it to the build settings.
    //        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    //        SECTR_VC.WaitForVC();

    //        EditorBuildSettingsScene sectorSceneSettings = new EditorBuildSettingsScene(newAssetPath, true);
    //        bool sceneExists = false;
    //        foreach (EditorBuildSettingsScene oldScene in sceneSettings)
    //        {
    //            if (oldScene.path == newAssetPath)
    //            {
    //                sceneExists = true;
    //                oldScene.enabled = true;
    //                break;
    //            }
    //        }
    //        if (!sceneExists)
    //        {
    //            sceneSettings.Add(sectorSceneSettings);
    //        }
    //        string[] pathParts = newAssetPath.Split('/');
    //        string sectorPath = pathParts[pathParts.Length - 1].Replace(".unity", "");

    //        // Update the master scene with exported info.
    //        EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
    //        SECTR_VC.WaitForVC();

    //        sector = _FindSectorByName(newAssetPath);
    //        sector.name = originalSectorName;

    //        DeleteExportedSector(sector);

    //        // Make sure Sectors has a Chunk
    //        SECTR_Chunk newChunk = sector.GetComponent<SECTR_Chunk>();
    //        if (!newChunk)
    //        {
    //            newChunk = sector.gameObject.AddComponent<SECTR_Chunk>();
    //        }
    //        newChunk.ScenePath = sectorPath;
    //        newChunk.NodeName = newAssetPath;
    //        newChunk.enabled = true;

    //        //Remove static flag if floating point fix is active
    //        if (SECTR_FloatingPointFix.IsActive)
    //        {
    //            sector.gameObject.isStatic = false;
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.BatchingStatic, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.LightmapStatic, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.NavigationStatic, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.OccludeeStatic, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.OccluderStatic, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.OffMeshLinkGeneration, false);
    //            //SECTR_Utils.SetStaticEditorFlag(sector.gameObject, StaticEditorFlags.ReflectionProbeStatic, false);
    //        }


    //        // Disable a TerrainComposer node if there is one.
    //        MonoBehaviour terrainNeighbors = sector.GetComponent("TerrainNeighbors") as MonoBehaviour;

    //        if (terrainNeighbors)
    //        {
    //            terrainNeighbors.enabled = false;
    //        }

    //        // Save off the accumulated build settings
    //        EditorBuildSettings.scenes = sceneSettings.ToArray();
    //        AssetDatabase.Refresh();

    //        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    //        SECTR_VC.WaitForVC();

    //        return true;
    //    }

    #endregion;


    /// Exports the specific Sector into an external level file, deleting the current scene copy in the process. Safe to call from command line. 
    /// <param name="sector">The Sector to export.</param>
    /// <param name="recycleScenes">If set to true, Sectr will re-use & empty existing scenes, rather than deleting the old ones and creating them from scratch.</param>
    /// <returns>Returns true if Sector was successfully exported, false otherwise.</returns>
    public static bool ExportToChunk(SECTR_Sector sector)
    {
        if (string.IsNullOrEmpty(SECTR_Asset.CurrentScene()))
        {
            Debug.LogError("Scene must be saved before export.");
            return false;
        }

        if (sector == null)
        {
            Debug.LogError("Cannot export null Sector.");
            return false;
        }

        if (!sector.gameObject.activeInHierarchy)
        {
            Debug.LogError("Cannot export inactive Sectors.");
            return false;
        }

        if (!sector.gameObject.isStatic && !SECTR_FloatingPointFix.IsActive)
        {
            Debug.Log("Skipping export of dynamic sector" + sector.name + ".");
            return true;
        }

        if (sector.Frozen)
        {
            // Already exported
            Debug.Log("Skipping frozen sector " + sector.name);
            return true;
        }


        string sceneDir;
        string sceneName;
        string exportDir = SECTR_Asset.MakeExportFolder("Chunks", false, out sceneDir, out sceneName);
        if (string.IsNullOrEmpty(exportDir))
        {
            Debug.LogError("Could not create Chunks folder.");
            return false;
        }

        // Delete the previous export, if there is one.
        // Prevents duplicate names piling up.
        // Only delete if we don't recycle old scenes!

        bool recycleScenes = EditorPrefs.GetBool(SECTR.PWSECTRPrefKeys.m_recycleScenesOnExport);

        SECTR_Chunk oldChunk = sector.GetComponent<SECTR_Chunk>();
        if (oldChunk && !recycleScenes)
        {
            AssetDatabase.DeleteAsset(oldChunk.NodeName);
            SECTR_VC.WaitForVC();
        }

        // Sectors are not guaranteed to be uniquely named, so always generate a unique name. 
        string originalSectorName = sector.name;
        string newAssetPath = "";
        if (oldChunk && recycleScenes)
        {
            newAssetPath = oldChunk.NodeName;
        }
        else
        {
            newAssetPath = AssetDatabase.GenerateUniqueAssetPath(exportDir + sceneName + "_" + originalSectorName + ".unity");
        }



        //sector.DisonnectTerrainNeighbors();

        //Copy the original sector
        GameObject sectorCopyGameObject = Object.Instantiate(sector.gameObject);
        sectorCopyGameObject.name = newAssetPath;

        //Remove all the children of the copy, we will instead reparent the original 
        //children to the copy to keep the prefab state in the exported sector.
        //Processing the transform collection backwards to prevent issues from deleting while iterating
        for(int i=sectorCopyGameObject.transform.childCount - 1; i>=0;i--)
        {
            DeleteObject(sectorCopyGameObject.transform.GetChild(i).gameObject);
        }

        // Freeze the original sector to preserve bounds while children are copied over
        sector.Frozen = true;

        //Reparent all children from the original to the copy
        //Using a list to store childs to reparent them in the same order as they were in the original sector
        List<Transform> childTransforms = new List<Transform>();
        for (int i = 0; i < sector.transform.childCount; i++)
        {
            childTransforms.Add(sector.transform.GetChild(i));
        }
        sector.transform.DetachChildren();

        foreach (Transform t in childTransforms)
        {
            t.parent = sectorCopyGameObject.transform;
        }

        //Remove all terrain references in the copy as this will only cause warnings when saving the scene
        SECTR_Sector sectrCopy = sectorCopyGameObject.GetComponent<SECTR_Sector>();
        sectrCopy.TopTerrain = null;
        sectrCopy.BottomTerrain = null;
        sectrCopy.LeftTerrain = null;
        sectrCopy.RightTerrain = null;

        // Make sure to force update all members so that membership info is correct.
        List<SECTR_Member> allMembers = FindAllOfType<SECTR_Member>();
        for (int memberIndex = 0; memberIndex < allMembers.Count; ++memberIndex)
        {
            allMembers[memberIndex].ForceUpdate(true);
        }

        // Multi-sector members need to stay in the master scene.
        foreach (SECTR_Member member in allMembers)
        {
            if (member.Sectors.Count > 1 && member.transform.IsChildOf(sectorCopyGameObject.transform))
            {
                bool unparentMember = true;

                // Only affect the first member in the hierarchy below the sector
                Transform parent = member.transform.parent;
                while (parent != sectorCopyGameObject.transform)
                {
                    if (parent.GetComponent<SECTR_Member>() != null)
                    {
                        unparentMember = false;
                        break;
                    }
                    parent = parent.parent;
                }

                if (unparentMember)
                {
#if UNITY_2018_3_OR_NEWER
                    if (PrefabUtility.GetPrefabInstanceStatus(sectorCopyGameObject.gameObject) != PrefabInstanceStatus.NotAPrefab)
#else
                            if (PrefabUtility.GetPrefabType(sector.gameObject) != PrefabType.None)
#endif
                    {
                        Debug.LogWarning("Export is unparenting shared member " + member.name + " from prefab Sector " + sector.name + ". This will break the prefab.");
                    }

                    member.transform.parent = null;
                }
            }
        }

        GameObject dummyParent = new GameObject(newAssetPath);
        SECTR_ChunkRef chunkRef = dummyParent.AddComponent<SECTR_ChunkRef>();
        chunkRef.RealSector = sectorCopyGameObject.transform;
        sectorCopyGameObject.transform.parent = dummyParent.transform;

        // If the sector has a chunk marked for re-use, perform some special work.
        SECTR_Chunk originalChunk = sectorCopyGameObject.GetComponent<SECTR_Chunk>();
        if (originalChunk && originalChunk.ExportForReuse)
        {
            chunkRef.Recentered = true;
            sectorCopyGameObject.transform.localPosition = Vector3.zero;
            sectorCopyGameObject.transform.localRotation = Quaternion.identity;
            sectorCopyGameObject.transform.localScale = Vector3.one;
            sectorCopyGameObject.gameObject.SetActive(false);
        }

        // Rename the real chunk root with a clear name.
        sectorCopyGameObject.name = originalSectorName + "_Chunk";

        // Strip off any functional objects that will be preserved in the root scene.
        // Destroy the chunk first because it has dependencies on Sector.
        DeleteObject(originalChunk);
        Component[] components = sectorCopyGameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component.GetType().IsSubclassOf(typeof(MonoBehaviour)) &&
               component.GetType() != typeof(Terrain) && component.GetType() != typeof(SECTR_LightmapRef))
            {
                DeleteObject(component);
            }
        }

        // Re-add a member that will persist all of the references and save us work post load.
        SECTR_Member refMember = sectorCopyGameObject.AddComponent<SECTR_Member>();
        refMember.NeverJoin = true;
        refMember.BoundsUpdateMode = SECTR_Member.BoundsUpdateModes.Static;
        refMember.ForceUpdate(true);

        string[] pathParts = newAssetPath.Split('/');
        string sectorPath = pathParts[pathParts.Length - 1].Replace(".unity", "");

        //Copy Render settings for the new scene to prevent warnings for unequal lighting settings across scenes
        var ambientEquatorColor = RenderSettings.ambientEquatorColor;
        var ambientGroundColor = RenderSettings.ambientGroundColor;
        var ambientIntensity = RenderSettings.ambientIntensity;
        var ambientLight = RenderSettings.ambientLight;
        var ambientMode = RenderSettings.ambientMode;
        var ambientProbe = RenderSettings.ambientProbe;
        var ambientSkyColor = RenderSettings.ambientSkyColor;
#if UNITY_2022_1_OR_NEWER
        var customReflection = RenderSettings.customReflectionTexture; 
#else
        var customReflection = RenderSettings.customReflection;
#endif
        var defaultReflectionMode = RenderSettings.defaultReflectionMode;
        var defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
        var flareFadeSpeed = RenderSettings.flareFadeSpeed;
        var flareStrength = RenderSettings.flareStrength;
        var fog = RenderSettings.fog;
        var fogColor = RenderSettings.fogColor;
        var fogDensity = RenderSettings.fogDensity;
        var fogEndDistance = RenderSettings.fogEndDistance;
        var fogMode = RenderSettings.fogMode;
        var fogStartDistance = RenderSettings.fogStartDistance;
        var haloStrength = RenderSettings.haloStrength;
        var reflectionBounces = RenderSettings.reflectionBounces;
        var reflectionIntensity = RenderSettings.reflectionIntensity;
        var skybox = RenderSettings.skybox;
        var subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
        //var sun = RenderSettings.sun;


        Scene newScene = new Scene();
        if (oldChunk && recycleScenes)
        {
            //load in the existing scene that is to be recycled and clear all contents
            newScene = EditorSceneManager.OpenScene(newAssetPath, OpenSceneMode.Additive);
            GameObject[] allGOs = Object.FindObjectsOfType<GameObject>();
            for (int i = allGOs.Length-1; i >= 0; i--)
            {
                if(allGOs[i]!= null && allGOs[i].scene == newScene)
                    DeleteObject(allGOs[i]);
            }
        }
        else
        {
            //No recycling - Create a new scene from scratch
            newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }
        
        EditorSceneManager.MoveGameObjectToScene(dummyParent, newScene);

        Scene originalScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SetActiveScene(newScene);

        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientProbe = ambientProbe;
        RenderSettings.ambientSkyColor = ambientSkyColor;
#if UNITY_2022_1_OR_NEWER
        RenderSettings.customReflectionTexture = customReflection;
#else
        RenderSettings.customReflection = customReflection;
#endif
        RenderSettings.defaultReflectionMode = defaultReflectionMode;
        RenderSettings.defaultReflectionResolution = defaultReflectionResolution;
        RenderSettings.flareFadeSpeed = flareFadeSpeed;
        RenderSettings.flareStrength = flareStrength;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogEndDistance = fogEndDistance;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.haloStrength = haloStrength;
        RenderSettings.reflectionBounces = reflectionBounces;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.skybox = skybox;
        RenderSettings.subtractiveShadowColor = subtractiveShadowColor;

        //switch back to main scene
        EditorSceneManager.SetActiveScene(originalScene);

        EditorSceneManager.SaveScene(newScene, newAssetPath);
        EditorSceneManager.CloseScene(newScene, true);

        // Make sure original Sector has a Chunk
        SECTR_Chunk newChunk = sector.GetComponent<SECTR_Chunk>();
        if (!newChunk)
        {
            newChunk = sector.gameObject.AddComponent<SECTR_Chunk>();
        }

        newChunk.ScenePath = sectorPath;
        newChunk.NodeName = newAssetPath;
        newChunk.enabled = true;


        DeleteExportedSector(sector);
        List<EditorBuildSettingsScene> sceneSettings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EditorBuildSettingsScene sectorSceneSettings = new EditorBuildSettingsScene(newAssetPath, true);
        bool sceneExists = false;
        foreach (EditorBuildSettingsScene oldScene in sceneSettings)
        {
            if (oldScene.path == newAssetPath)
            {
                sceneExists = true;
                oldScene.enabled = true;
                break;
            }
        }
        if (!sceneExists)
        {
            sceneSettings.Add(sectorSceneSettings);
        }

        

        // Save off the accumulated build settings
        EditorBuildSettings.scenes = sceneSettings.ToArray();
        AssetDatabase.Refresh();



        return true;
    }

    public static void DeleteObject(Object objectToDelete)
    {
        if (objectToDelete == null)
        {
            return;
        }
#if UNITY_2018_3_OR_NEWER
        //check if object is within a prefab
        if (PrefabUtility.IsPartOfAnyPrefab(objectToDelete))
        {
            GameObject rootObject = PrefabUtility.GetOutermostPrefabInstanceRoot(objectToDelete);
            if (rootObject != null)
            {
                UnityEditor.PrefabUtility.UnpackPrefabInstance(rootObject, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
            }
            GameObject.DestroyImmediate(objectToDelete);
        }
        else
        {
            GameObject.DestroyImmediate(objectToDelete);
        }

#else
        GameObject.DestroyImmediate(objectToDelete);
#endif
    }

    public static void DeleteExportedSector(SECTR_Sector sector)
    {
        // Force update all members
        List<SECTR_Member> allMembers = FindAllOfType<SECTR_Member>();
        for (int memberIndex = 0; memberIndex < allMembers.Count; ++memberIndex)
        {
            allMembers[memberIndex].ForceUpdate(true);
        }

        // Remove everything from the Sector except for the transform and any MonoBehavior
        // (but also nuke Terrain which is a MonoBehavior but really isn't)
        Component[] components = sector.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != sector.transform &&
               (!component.GetType().IsSubclassOf(typeof(MonoBehaviour)) || component.GetType() == typeof(Terrain) || component.GetType() == typeof(SECTR_LightmapRef)))
            {
                DeleteObject(component);
            }
        }

        // Multi-sector members stay in the master scene, so unparent them.
        List<SECTR_Member> sharedMembers = new List<SECTR_Member>();
        foreach (SECTR_Member member in allMembers)
        {
            if (member.Sectors.Count > 1 && member.transform.parent && member.transform.parent.IsChildOf(sector.transform))
            {
                bool unparentMember = true;

                // Only unparent the first member in the heirarchy below the transform.
                Transform parent = member.transform.parent;
                while (parent != sector.transform)
                {
                    if (parent.GetComponent<SECTR_Member>() != null)
                    {
                        unparentMember = false;
                        break;
                    }
                    parent = parent.parent;
                }

                if (unparentMember)
                {
                    member.transform.parent = null;
                    sharedMembers.Add(member);
                }
            }
        }

        // Destroy all exported children
        //List<Transform> allXforms = FindAllOfType<Transform>();
        //foreach (Transform transform in allXforms)
        //{
        //    if (transform && transform.IsChildOf(sector.transform) && transform != sector.transform)
        //    {
        //        DeleteObject(transform.gameObject);
        //    }
        //}

        var children = sector.transform.Cast<Transform>().ToArray();
        foreach (var child in children)
        {
            DeleteObject(child.gameObject);
        }


        // Now reparent the global objects
        foreach (SECTR_Member member in sharedMembers)
        {
            member.transform.parent = sector.transform;
        }
        Resources.UnloadUnusedAssets();

        // Freeze the sector to preserve bounds but prevent updates.
        sector.Frozen = true;
    }





    /// Exports all of the Sectors in the scene, with user prompts and other helpful dialogs.
    public static void ExportSceneChunksUI()
    {
        if (string.IsNullOrEmpty(SECTR_Asset.CurrentScene()))
        {
            EditorUtility.DisplayDialog("Export Error", "Cannot export from a scene that's never been saved.", "Ok");
            return;
        }

        if (!SECTR_VC.CheckOut(SECTR_Asset.CurrentScene()))
        {
            EditorUtility.DisplayDialog("Export Error", "Could not check out " + SECTR_Asset.CurrentScene() + ". Export aborted.", "Ok");
            return;
        }

        string sceneDir;
        string sceneName;
        string exportDir = SECTR_Asset.MakeExportFolder("Chunks", false, out sceneDir, out sceneName);
        if (string.IsNullOrEmpty(exportDir))
        {
            EditorUtility.DisplayDialog("Export Error", "Could not create Chunks folder. Aborting Export.", "Ok");
            return;
        }

        SECTR_Loader[] loaders = (SECTR_Loader[])GameObject.FindObjectsOfType(typeof(SECTR_Loader));
        if (loaders.Length == 0 && !EditorUtility.DisplayDialog("No Loaders", "This scene has no loaders. Are you sure you wish to export?", "Ok", "Cancel"))
        {
            return;
        }

        if (SECTR_FloatingPointFix.IsActive)
        {
            string foundObjectName = "";
            int count = 0;
            if (CheckForStaticObjectsInSectors(ref foundObjectName, ref count, false))
            {
                if (!EditorUtility.DisplayDialog("Static Objects Found", "You are using the floating point fix in your scene, but there are still static objects in your sectors.\n\n The first static object found was: " + foundObjectName + "\n\nThe floating point fix requires all objects inside the sectors to be non-static, otherwise they can't be shifted during runtime. Continue Export anyways?", "Continue Anyways", "Cancel Export"))
                {
                    return;
                }
            }
        }

        int backupValue = _ShowBackupPrompt("EXPORT");
        if (backupValue != 1)
        {
            ExportSceneChunks();
        }
    }

    public static bool CheckForStaticObjectsInSectors(ref string foundObjectName, ref int objectCount, bool searchAll)
    {
        foundObjectName = "";
        objectCount = 0;

        foreach (SECTR_Sector sector in GameObject.FindObjectsOfType<SECTR_Sector>())
        {
            foreach (Transform t in sector.transform)
            {
                if (t.gameObject.isStatic)
                {
                    objectCount++;
                    foundObjectName = t.name;
                    if (!searchAll)
                    {
                        return true;
                    }
                }
            }
        }
        if (objectCount > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    public static bool CheckForWorldSpaceParticleSystemsInSectors(ref string foundObjectName, ref int objectCount, bool searchAll)
    {
        foundObjectName = "";
        objectCount = 0;

        foreach (SECTR_Sector sector in GameObject.FindObjectsOfType<SECTR_Sector>())
        {
            foreach (Transform t in sector.transform)
            {
                ParticleSystem ps = t.GetComponent<ParticleSystem>();
                if (ps && ps.main.simulationSpace == ParticleSystemSimulationSpace.World && t.GetComponent<SECTR_FloatingPointFixParticleSystem>() == null)
                {
                    objectCount++;
                    foundObjectName = t.name;
                    if (!searchAll)
                    {
                        return true;
                    }
                }
            }
        }
        if (objectCount > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    /// Exports all Sectors in the scene. Safe to call from the command line.
    public static void ExportSceneChunks()
    {
        // Create a progress bar, because we're friendly like that.
        string progressTitle = "Chunking Level For Streaming";
        EditorUtility.DisplayProgressBar(progressTitle, "Preparing", 0);

        List<EditorBuildSettingsScene> sceneSettings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EditorBuildSettingsScene rootSceneSettings = new EditorBuildSettingsScene(SECTR_Asset.CurrentScene(), true);
        bool sceneExists = false;
        foreach (EditorBuildSettingsScene oldScene in sceneSettings)
        {
            if (oldScene.path == SECTR_Asset.CurrentScene())
            {
                sceneExists = true;
                oldScene.enabled = true;
                break;
            }
        }
        if (!sceneExists)
        {
            sceneSettings.Add(rootSceneSettings);
            EditorBuildSettings.scenes = sceneSettings.ToArray();
        }

        // Export each sector to an individual file.
        // Inner loop reloads the scene, and Sector creation order is not deterministic, 
        // so it requires multiple passes through the list.
        int numSectors = SECTR_Sector.All.Count;
        int progress = 0;
        int unfrozenSectors = 0;

        // Figure out how many sectors we should be exporting.
        for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
        {
            SECTR_Sector sector = SECTR_Sector.All[sectorIndex];
            if (!sector.Frozen)
            {
                ++unfrozenSectors;
            }
        }

        while (progress < unfrozenSectors)
        {
            for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
            {
                SECTR_Sector sector = SECTR_Sector.All[sectorIndex];
                if (!sector.Frozen)
                {
                    EditorUtility.DisplayProgressBar(progressTitle, "Exporting " + sector.name, (float)progress / (float)unfrozenSectors);
                    ExportToChunk(sector);
                    ++progress;
                }
            }
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SECTR_VC.WaitForVC();

        // Cleanup
        EditorUtility.ClearProgressBar();
    }

    /// Imports all of the Sectors in the scene, with user prompts and other helpful dialogs.
    public static void ImportSceneChunksUI()
    {
        if (string.IsNullOrEmpty(SECTR_Asset.CurrentScene()))
        {
            EditorUtility.DisplayDialog("Import Error", "Cannot import into scene that's never been saved.", "Ok");
        }

        int backupValue = _ShowBackupPrompt("IMPORT");
        if (backupValue != 1)
        {
            ImportSceneChunks();
        }
    }

    /// Imports all exported Sectors into the scene. Safe to call from the command line.
    public static void ImportSceneChunks()
    {
        int numSectors = SECTR_Sector.All.Count;
        for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
        {
            SECTR_Sector sector = SECTR_Sector.All[sectorIndex];
            if (sector.Frozen)
            {
                EditorUtility.DisplayProgressBar("Importing Scene Chunks", "Importing " + sector.name, (float)sectorIndex / (float)numSectors);
                ImportFromChunk(sector);
            }
        }
        if (SECTR_VC.CheckOut(SECTR_Asset.CurrentScene()))
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SECTR_VC.WaitForVC();
        }
        EditorUtility.ClearProgressBar();
    }

    /// Reverts all of the imported Sectors in the scene, with user prompts and other helpful dialogs.
    public static void RevertSceneChunksUI()
    {
        int backupValue = _ShowBackupPrompt("REVERT");
        if (backupValue != 1)
        {
            RevertSceneChunks();
        }
    }

    /// Reverts all imported Sectors into the scene. Safe to call from the command line.
    public static void RevertSceneChunks()
    {
        int numSectors = SECTR_Sector.All.Count;
        for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
        {
            SECTR_Sector sector = SECTR_Sector.All[sectorIndex];
            EditorUtility.DisplayProgressBar("Reverting Scene Chunks", "Reverting " + sector.name, (float)sectorIndex / (float)numSectors);
            RevertChunk(sector);
        }
        if (SECTR_VC.CheckOut(SECTR_Asset.CurrentScene()))
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            SECTR_VC.WaitForVC();
        }
        EditorUtility.ClearProgressBar();
    }

    public static void RevertChunk(SECTR_Sector sector)
    {
        SECTR_Chunk chunk = sector.GetComponent<SECTR_Chunk>();
        if (!sector.Frozen && chunk &&
            System.IO.File.Exists(SECTR_Asset.UnityToOSPath(chunk.NodeName)))
        {
            DeleteExportedSector(sector);
            chunk.enabled = true;
            EditorSceneManager.CloseScene(EditorSceneManager.GetSceneByPath(chunk.NodeName), true);
        }
    }

    /// Writes out the current scene's Sector/Portal graph as a .dot file
    /// which can be visualized in programs like GraphVis and the like.
    public static void WriteGraphDot()
    {
        if (!string.IsNullOrEmpty(SECTR_Asset.CurrentScene()))
        {
            string sceneDir;
            string sceneName;
            SECTR_Asset.GetCurrentSceneParts(out sceneDir, out sceneName);
            sceneName = sceneName.Replace(".unity", "");

            string graphFile = SECTR_Graph.GetGraphAsDot(sceneName);

            string path = sceneDir + sceneName + "_SECTR_Graph.dot";
            File.WriteAllText(SECTR_Asset.UnityToOSPath(path), graphFile);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
            EditorUtility.FocusProjectWindow();
        }
    }
    #endregion

    #region Private Methods
    private static int _ShowBackupPrompt(string operationType)
    {
        int dialogValue = EditorUtility.DisplayDialogComplex("Make Backup?", "This operation will significantly modify your scene. Would you like to make a backup first?", "Yes", "Cancel", "No");
        if (dialogValue == 0)
        {
            string sceneDir;
            string sceneName;
            SECTR_Asset.SplitPath(SECTR_Asset.CurrentScene(), out sceneDir, out sceneName);
            sceneName = sceneName.Replace(".unity", "");
            string targetDir = SECTR_Asset.MakeExportFolder("Backups", false, out sceneDir, out sceneName);
            string dateTime = System.DateTime.Now.ToString("yyyyMMdd-HH-mm");
            
            AssetDatabase.CopyAsset(SECTR_Asset.CurrentScene(), targetDir + sceneName + "_" + operationType + "_" + dateTime + ".unity");
            AssetDatabase.Refresh();
            SECTR_VC.WaitForVC();
        }
        return dialogValue;
    }

    private static List<T> FindAllOfType<T>() where T : Component
    {
        List<T> sceneObjects = new List<T>();
        T[] everything = (T[])Resources.FindObjectsOfTypeAll(typeof(T));
        foreach (T item in everything)
        {
            // Be very sure that we're not destroying a resource.
            if ((item.gameObject.hideFlags & HideFlags.NotEditable) == 0 &&
                !EditorUtility.IsPersistent(item.gameObject) &&
                   !EditorUtility.IsPersistent(item.transform.gameObject) &&
                   string.IsNullOrEmpty(AssetDatabase.GetAssetPath(item.gameObject)) &&
                   string.IsNullOrEmpty(AssetDatabase.GetAssetPath(item.transform.root.gameObject)))
            {
                sceneObjects.Add(item);
            }
        }
        return sceneObjects;
    }

    private static SECTR_Sector _FindSectorByName(string name)
    {
        int numSectors = SECTR_Sector.All.Count;
        for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
        {
            SECTR_Sector sector = SECTR_Sector.All[sectorIndex];
            if (sector.name == name)
            {
                return sector;
            }
        }
        return null;
    }
    #endregion
}
