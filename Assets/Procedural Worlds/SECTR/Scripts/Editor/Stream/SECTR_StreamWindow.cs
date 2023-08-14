// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public enum StreamWindowSortMethod { Name, HierarchyPosition }

public class SECTR_StreamWindow : SECTR_Window
{
    private Vector2 scrollPosition;
    private string sectorSearch = "";
    private SECTR_Sector selectedSector = null;

    private StreamWindowSortMethod sortMethod = StreamWindowSortMethod.Name;


    #region Unity Interface
    protected override void OnGUI()
    {
        base.OnGUI();
        EditorGUILayout.BeginVertical();
        string nullSearch = null;
     
        List<SECTR_Sector> sortedSectors = new List<SECTR_Sector>(SECTR_Sector.All);
        switch (sortMethod)
        {
            case StreamWindowSortMethod.Name:
                sortedSectors.Sort(delegate (SECTR_Sector a, SECTR_Sector b) { return a.name.CompareTo(b.name); });
                break;
            case StreamWindowSortMethod.HierarchyPosition:
                sortedSectors.Sort(delegate (SECTR_Sector a, SECTR_Sector b) { return SortByHierarchy(a, b); });
                break;
            default:
                sortedSectors.Sort(delegate (SECTR_Sector a, SECTR_Sector b) { return a.name.CompareTo(b.name); });
                break;
        }


        int numSectors = sortedSectors.Count;
        bool sceneHasSectors = numSectors > 0;
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        headerStyle.alignment = TextAnchor.MiddleLeft ;
        GUILayout.FlexibleSpace();
        GUILayout.Label("SECTORS", headerStyle);
		if(sectorSearch != null)
		{
			GUI.SetNextControlName("SECTORS_Header");
            sectorSearch = EditorGUILayout.TextField(sectorSearch, searchBoxStyle, GUILayout.Width(100));
			if(GUILayout.Button("", searchCancelStyle))
			{
                // Remove focus if cleared
                sectorSearch = "";
				GUI.FocusControl(null);
			}
		}
        GUILayout.Label("SORT BY", headerStyle);
        sortMethod = (StreamWindowSortMethod)EditorGUILayout.EnumPopup(sortMethod);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        

        //Rect r = EditorGUILayout.BeginVertical();
        //r.y += numSectors * lineHeight;
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = false;
        if (!sceneHasSectors)
        {
            Rect r = EditorGUILayout.BeginVertical();
            r.y += lineHeight;
            r.height = 50f;
            GUI.Button(r, sceneHasSectors ? "" : "Current Scene has no Sectors!");
            EditorGUILayout.EndVertical();
        }

        GUI.enabled = wasEnabled;
        bool allExported = true;
        bool allImported = true;
        bool someImported = false;
        SECTR_Sector newSelectedSector = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<SECTR_Sector>() : null; ;
        bool mouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 0;
        if (mouseDown)
        {
            newSelectedSector = null;
        }

        for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
        {
            SECTR_Sector sector = sortedSectors[sectorIndex];
            if (sector.name.ToLower().Contains(sectorSearch.ToLower()))
            {
                bool selected = sector == selectedSector;
                Rect clipRect = EditorGUILayout.BeginHorizontal();
                if (selected)
                {
                    Rect selectionRect = clipRect;
                    selectionRect.y += 1;
                    selectionRect.height -= 1;
                    GUI.Box(selectionRect, "", selectionBoxStyle);
                }
                if (sector.Frozen)
                {
                    allImported = false;
                }
                else
                {
                    if (sector.GetComponent<SECTR_Chunk>())
                    {
                        someImported = true;
                    }
                    allExported = false;
                }

                elementStyle.normal.textColor = selected ? Color.white : UnselectedItemColor;
                elementStyle.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.LabelField(sector.name, elementStyle);

                EditorGUILayout.EndHorizontal();
                float buttonWidth = 50;
                if (sector.gameObject.isStatic)
                {
                   
                    SECTR_Chunk chunk = sector.GetComponent<SECTR_Chunk>();
                    bool alreadyExported = chunk && System.IO.File.Exists(SECTR_Asset.UnityToOSPath(chunk.NodeName));
                    if (sector.Frozen)
                    {
                        // Import
                        if (alreadyExported &&
                           GUI.Button(new Rect(0, clipRect.yMin, buttonWidth, clipRect.height), new GUIContent("Import", "Imports this Sector into the scene.")))
                        {
                            SECTR_StreamExport.ImportFromChunk(sector);
                            break;
                        }
                    }
                    else
                    {
                        // Revert
                        if (alreadyExported &&
                           GUI.Button(new Rect(0, clipRect.yMin, buttonWidth, clipRect.height), new GUIContent("Revert", "Discards changes to this Sector.")))
                        {
                            SECTR_StreamExport.RevertChunk(sector);
                            break;
                        }
                        // Export
                        if (GUI.Button(new Rect(clipRect.xMax - buttonWidth, clipRect.yMin, buttonWidth, clipRect.height), new GUIContent("Export", "Exports this Sector into a Chunk scene.")))
                        {
                            SECTR_StreamExport.ExportToChunk(sector);

                            if (EditorPrefs.GetBool(SECTR.PWSECTRPrefKeys.m_autoSaveOnExport))
                            {
                                //Save original scene after export
                                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                            }
                            break;
                        }
                    }
                }

                Rect mouseOverRect = new Rect(clipRect.x + buttonWidth, clipRect.y, clipRect.width - (buttonWidth * 2), clipRect.height);
                if (mouseDown && mouseOverRect.Contains(Event.current.mousePosition))
                {
                    newSelectedSector = sector;
                }
            }
        }
        if (newSelectedSector != selectedSector)
        {
            selectedSector = newSelectedSector;
            Selection.activeGameObject = selectedSector ? selectedSector.gameObject : null;
            if (SceneView.lastActiveSceneView)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Repaint();
        }
        EditorGUILayout.EndScrollView();
        //EditorGUILayout.EndVertical();


        DrawHeader("FLOATING POINT FIX", ref nullSearch, 0, true);




        if (!SECTR_FloatingPointFix.IsActive)
        {
            if (GUILayout.Button(new GUIContent("Activate Floating Point Fix", "Adds the floating point fix component to the first loader that is found in the scene.")))
            {
                SECTR_Loader firstLoader = FindObjectOfType<SECTR_Loader>();
                if (firstLoader)
                {
                    if (EditorUtility.DisplayDialog("Activate Floating Point Fix", "Do you want to activate an automatic fix for floating point precision issues?\n\nThis will add the floating point fix component to the following loader in your scene:\n\n" + firstLoader.name, "OK", "Cancel"))
                    {
                        firstLoader.gameObject.AddComponent<SECTR_FloatingPointFix>();
                        GUI.enabled = true;
#if GAIA_PRESENT
                        //Look for potential Gaia optimizations
                        GameObject gaiaEnvironment = GameObject.Find("Gaia Environment");
                        if (gaiaEnvironment)
                        {
                            if (EditorUtility.DisplayDialog("Gaia Environment Found", "It looks like you are using a Gaia Terrain in this scene, do you want to perform the following optimizations for the floating point fix?\n\n-Deactivate the Camera Reflection Probe\n\n-Mark the water plane and effects as floating point fix member so they will work with the fix.", "Perform Optimizations", "Cancel"))
                            {
                                Transform waterSampleTransform = gaiaEnvironment.transform.Find("Ambient Water Samples");
                                if (waterSampleTransform)
                                {
                                    Transform t = waterSampleTransform.Find("Camera Reflection Probe");
                                    if (t != null)
                                        t.gameObject.SetActive(false);
                                    t = waterSampleTransform.Find("Underwater PostFX");
                                    if (t != null)
                                        t.gameObject.AddComponent<SECTR_FloatingPointFixMember>();
                                    t = waterSampleTransform.Find("Underwater Transition PostFX");
                                    if (t != null)
                                        t.gameObject.AddComponent<SECTR_FloatingPointFixMember>();
                                    t = waterSampleTransform.Find("Ambient Water Sample");
                                    if (t != null)
                                        t.gameObject.AddComponent<SECTR_FloatingPointFixMember>();
                                }
                            }
                        }

#endif
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("No Loader found!", "Found no Sectr loader in the scene, please add any loader component first to the camera or player character in your scene.", "OK");
                    GUI.enabled = false;
                }
            }
            else
            {
                GUI.enabled = false;
            }
        }
        else
        {
            if (GUILayout.Button(new GUIContent("Deactivate Floating Point Fix", "Removes the floating point fix component from the scene.")))
            {
                if (EditorUtility.DisplayDialog("Deactivate Floating Point Fix", "Do you want to deactivate the floating point for this scene? This will remove all instances from the floating point fix component from this scene.", "OK", "Cancel"))
                {
                    var allFloatingPointFixes = FindObjectsOfType<SECTR_FloatingPointFix>();

                    for (int i = allFloatingPointFixes.Length - 1; i >= 0; i--)
                    {
                        DestroyImmediate(allFloatingPointFixes[i]);
                    }

                }
            }
            GUI.enabled = true;
        }

        if (GUILayout.Button(new GUIContent("Find and Fix Static Objects", "Checks for static objects in your sectors which can create issues when using the floating point fix.")))
        {
            bool foundStatic = false;
            int objectCount = 0;
            string objectName = "";

            foundStatic = SECTR_StreamExport.CheckForStaticObjectsInSectors(ref objectName, ref objectCount, true);

            if (foundStatic)
            {
                if (EditorUtility.DisplayDialog("Found Static Objects", "Found " + objectCount.ToString() + " static objects in the currently loaded Sectors. First static object found was:\n\n" + objectName + "\n\nDo you want to remove the static flag on these objects automatically?", "Remove all static flags", "Cancel"))
                {
                    int fixedObjectCount = 0;

                    foreach (SECTR_Sector sector in GameObject.FindObjectsOfType<SECTR_Sector>())
                    {
                        foreach (Transform t in sector.transform)
                        {
                            if (t.gameObject.isStatic)
                            {
                                t.gameObject.isStatic = false;
                                fixedObjectCount++;
                            }
                        }
                    }
                    EditorUtility.DisplayDialog("Done Removing Static flags", "Removed " + fixedObjectCount.ToString() + " static flags from objects.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Static Objects Found", "Found no static objects in the currently loaded Sectors.", "OK");
            }

        }
        if (GUILayout.Button(new GUIContent("Find and Fix World Space Particle Systems", "Looks for particle systems that simulate in world space which can lead to issues when using the floating point fix.")))
        {
            bool foundWSParticles = false;
            int objectCount = 0;
            string objectName = "";

            foundWSParticles = SECTR_StreamExport.CheckForWorldSpaceParticleSystemsInSectors(ref objectName, ref objectCount, true);
            if (foundWSParticles)
            {
                if (EditorUtility.DisplayDialog("Found Particle Systems in World Space", "Found " + objectCount.ToString() + " world space particle systems in the currently loaded Sectors. First particle system found was:\n\n" + objectName + "\n\nDo you want to add support for the floating point fix on these objects automatically?", "Add support to particle systems", "Cancel"))
                {
                    int fixedObjectCount = 0;

                    foreach (SECTR_Sector sector in GameObject.FindObjectsOfType<SECTR_Sector>())
                    {
                        foreach (Transform t in sector.transform)
                        {
                            ParticleSystem ps = t.GetComponent<ParticleSystem>();
                            if (ps && ps.main.simulationSpace == ParticleSystemSimulationSpace.World && t.GetComponent<SECTR_FloatingPointFixParticleSystem>() == null)
                            {
                                t.gameObject.AddComponent<SECTR_FloatingPointFixParticleSystem>();
                                fixedObjectCount++;
                            }
                        }
                    }
                    EditorUtility.DisplayDialog("Done Fixing Particle Systems", "Added fix to " + fixedObjectCount.ToString() + " particle systems.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No World Space Particle Systems Found", "Found no particle systems running in world space in the sectors (that don't have the floating point fix applied).", "OK");
            }
        }

        //string nullSearch = null;
        GUI.enabled = true;
        DrawHeader("EXPORT AND IMPORT", ref nullSearch, 0, true);
        wasEnabled = GUI.enabled;
        bool editMode = !EditorApplication.isPlaying && !EditorApplication.isPaused;
        GUI.enabled = sceneHasSectors && !allExported && wasEnabled && editMode;
        if (GUILayout.Button(new GUIContent("Export All Sectors", "Exports all static Sectors into Chunk scenes and prepares them for streaming.")))
        {
            SECTR_StreamExport.ExportSceneChunksUI();
            Repaint();
        }
        GUI.enabled = sceneHasSectors && !allImported && wasEnabled && editMode;
        if (GUILayout.Button(new GUIContent("Import All Sectors", "Imports all exported Chunks back into the scene.")))
        {
            SECTR_StreamExport.ImportSceneChunksUI();
            Repaint();
        }
        GUI.enabled = sceneHasSectors && !allExported && someImported && wasEnabled && editMode;
        if (GUILayout.Button(new GUIContent("Revert All Sectors", "Reverts all exported Chunks to their exported state.")))
        {
            SECTR_StreamExport.RevertSceneChunksUI();
            Repaint();
        }

       

        GUI.enabled = true;
        DrawHeader("LIGHTMAPPING", ref nullSearch, 0, true);
        GUI.enabled = sceneHasSectors && selectedSector && allExported && wasEnabled && editMode;
        if (GUILayout.Button(new GUIContent("Lightmap Selected Sector", "Lightmaps selected Sector in isolation.")))
        {
            if (EditorUtility.DisplayDialog("Confirm Lightmap Bake", "Are you sure you want to bake lightmaps for " + selectedSector.name +
                                           "? Its lighting will not be affected by any other Sectors.", "Yes", "No"))
            {
                string[] paths = new string[2];
                paths[0] = SECTR_Asset.CurrentScene();
                paths[1] = selectedSector.GetComponent<SECTR_Chunk>().NodeName;
                Lightmapping.BakeMultipleScenes(paths);
            }
        }

        GUI.enabled = sceneHasSectors && allExported && wasEnabled && editMode;
        if (GUILayout.Button(new GUIContent("Lightmap All Sectors", "Lightmaps all exported Chunks.")))
        {
            if (EditorUtility.DisplayDialog("Confirm Lightmap Bake", "Are you sure you want to bake lightmaps for all subscenes? This may take quite a while.", "Yes", "No"))
            {
                string[] paths = new string[numSectors + 1];
                paths[0] = SECTR_Asset.CurrentScene();
                for (int sectorIndex = 0; sectorIndex < numSectors; ++sectorIndex)
                {
                    paths[sectorIndex + 1] = sortedSectors[sectorIndex].GetComponent<SECTR_Chunk>().NodeName;
                }
                Lightmapping.BakeMultipleScenes(paths);
            }
        }
        GUI.enabled = true;
        DrawHeader("EXTRA", ref nullSearch, 0, true);
        GUI.enabled = sceneHasSectors;
        if (GUILayout.Button(new GUIContent("Export Sector Graph Visualization", "Writes out a .dot file of the Sector/Portal graph, which can be visualized in GraphViz.")))
        {
            SECTR_StreamExport.WriteGraphDot();
        }
        //EditorGUILayout.BeginHorizontal();
        //GUILayout.Toggle(EditorPrefs.GetBool(SECTR.PWSECTRPrefKeys.m_autoSaveOnExport), new GUIContent("Auto Save on Export", "Automatically save the scene whenever a sector is exported"));
        //GUI.enabled = wasEnabled;
        //GUILayout.Toggle(EditorPrefs.GetBool(SECTR.PWSECTRPrefKeys.m_recycleScenesOnExport), new GUIContent("Recycle Exported Scenes", "Re-use the existing scene chunks during export, rather than deleting & creating them from scratch. Recycling takes a bit more time on export by re-using the scenes, but does in return not re-create entries for new scenes in the build settings."));
        //GUI.enabled = wasEnabled;
        //EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private int SortByHierarchy(SECTR_Sector a, SECTR_Sector b)
    {
        int index = 0;
        int aIndex = 0;
        int bIndex = 0;

        bool aFound = false, bFound = false;


        List<GameObject> rootObjects = new List<GameObject>();
        Scene scene = SceneManager.GetActiveScene();
        scene.GetRootGameObjects(rootObjects);



        for (int i = 0; i < rootObjects.Count; ++i)
        {
            if (rootObjects[i] == a.gameObject)
            {
                aIndex = index;
                aFound = true;
            }
            if (rootObjects[i] == b.gameObject)
            {
                bIndex = index;
                bFound = true;
            }

            if (aFound && bFound)
            {
                break;
            }

            index++;

            foreach (Transform t in rootObjects[i].transform)
            {
                if (t.gameObject == a.gameObject)
                {
                    aIndex = index;
                    aFound = true;
                }
                if (t.gameObject == b.gameObject)
                {
                    bIndex = index;
                    bFound = true;
                }
                index++;
                if (aFound && bFound)
                {
                    break;
                }
            }
        }


        if (aIndex == bIndex) return 0;
        if (aIndex > bIndex) return 1; else return -1;

    }
    #endregion
}
