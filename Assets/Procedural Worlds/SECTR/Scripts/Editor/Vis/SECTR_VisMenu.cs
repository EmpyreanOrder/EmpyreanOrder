// Copyright (c) 2014 Make Code Now! LLC

using UnityEditor;
using UnityEngine;

public class SECTR_VisMenu : SECTR_Menu 
{
	const string rootPath = createMenuRootPath + "VIS/";
	const string createCullingCamera = rootPath + "Camera";
	const string createOccluder = rootPath + "Occluder";
	const string createLOD = rootPath + "LOD";
	const int createCullingCameraPriority = visPriority + 0;
	const int createOccluderPriority = visPriority + 5;
	const int createLODPriority = visPriority + 10;

	[MenuItem(createCullingCamera, false, createCullingCameraPriority)]
	public static void CreateCullingCameraCamera() 
	{
		string newObjectName = "SECTR Camera";
		string undoString = "Create " + newObjectName;
		if(Selection.activeGameObject && Selection.activeGameObject.GetComponent<Camera>())
		{
			if(Selection.activeGameObject.GetComponent<SECTR_CullingCamera>())
			{
				Debug.LogWarning("Selected Camera already has a SECTR CullingCamera.");
			}
			else
			{
				SECTR_CullingCamera newCullingCamera = Selection.activeGameObject.AddComponent<SECTR_CullingCamera>();
				SECTR_Undo.Created(newCullingCamera, undoString);
			}
		}
		else
		{
			GameObject newObject = CreateGameObject(newObjectName);
			newObject.AddComponent<SECTR_CullingCamera>();
			SECTR_Undo.Created(newObject, undoString);
			Selection.activeGameObject = newObject;
		}
	}

	[MenuItem(createOccluder, false, createOccluderPriority)]
	public static void CreateOccluder() 
	{
		string newObjectName = "SECTR Occluder";
		string undoString = "Create " + newObjectName;
		GameObject newObject = CreateGameObject(newObjectName);
		SECTR_Occluder newOccluder = newObject.AddComponent<SECTR_Occluder>();
		newOccluder.ForceEditHull = true;
		newOccluder.CenterOnEdit = true;
		SECTR_Undo.Created(newObject, undoString);
		Selection.activeGameObject = newObject;
	}

	[MenuItem(createLOD, false, createLODPriority)]
	public static void CreateLOD() 
	{
		string newObjectName = "SECTR LOD";
		string undoString = "Create " + newObjectName;
		GameObject newObject = CreateGameObject(newObjectName);
		newObject.AddComponent<SECTR_LOD>();
		int numSelected = Selection.gameObjects.Length;
		SECTR_Undo.Created(newObject, undoString);
		for(int selectedIndex = 0; selectedIndex < numSelected; ++selectedIndex)
		{
			SECTR_Undo.Parent(newObject, Selection.gameObjects[selectedIndex], undoString);
		}
		Selection.activeGameObject = newObject;
	}
}
