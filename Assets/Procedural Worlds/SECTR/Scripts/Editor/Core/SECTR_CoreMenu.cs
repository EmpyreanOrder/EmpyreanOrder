// Copyright (c) 2014 Make Code Now! LLC

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public class SECTR_CoreMenu : SECTR_Menu 
{
	const string rootCreatePath = createMenuRootPath + "CORE/";
	const string createSectorItem = rootCreatePath + "Sector #&s";
	const string createPortalItem = rootCreatePath + "Portal #&p";
	const string createMemberItem = rootCreatePath + "Member";
	const string createDoorItem = rootCreatePath + "Core Door";
	const int createSectorPriority = corePriority + 0;
	const int createPortalPriority = corePriority + 5;
	const int createMemberPriority = corePriority + 10;
	const int createDoorPriority = corePriority + 50;

	const string introWindowItem = windowMenuRootPath + "Quick Start";
	const int introWindowPriority = windowPriority;
	const string terrainWindowItem = windowMenuRootPath + "Terrain Window";
    const int terrainWindowPriority = windowPriority;
    //stores an Unix timestamp for when the next execution from the context menu is allowed
    private static double nextContextMenuExecutionAllowedTimeStamp;

    [MenuItem(createSectorItem, false, createSectorPriority)]
	public static void CreateSector()
	{

        //We check from where this function was called first
        // get call stack
        StackTrace stackTrace = new StackTrace();
        // get calling method name, if possible
        if (stackTrace != null && stackTrace.GetFrame(1) != null && stackTrace.GetFrame(1).GetMethod() != null)
        {
            if (stackTrace.GetFrame(1).GetMethod().Name.EndsWith("WithTemporaryContext"))
            {
                //This was called from a right click context menu, which means this function would be called once for all selected objects each.
                //We therefore only allow the first execution, and then "lock" the subsequent executions for a second. 
                if (nextContextMenuExecutionAllowedTimeStamp < SECTR_SectorUtils.GetUnixTimeStamp())
                {
                    //execution allowed, set the timestamp to lock subsequent executions
                    nextContextMenuExecutionAllowedTimeStamp = SECTR_SectorUtils.GetUnixTimeStamp() + 1000;
                }
                else
                {
                    //execution not allowed, end function
                    return;
                }

            }
        }

        List<GameObject> rootObjects = new List<GameObject>(8);
		List<GameObject> selectedObjects = new List<GameObject>(Selection.gameObjects);
		int numSelectedObjects = selectedObjects.Count;
		int selectedObjectIndex = 0;
		while(selectedObjectIndex < numSelectedObjects)
		{
			GameObject gameObject = selectedObjects[selectedObjectIndex];
			// auto remove portals if selected.
			if(gameObject.GetComponent<SECTR_Portal>())
			{
				selectedObjects.RemoveAt(selectedObjectIndex);
				--numSelectedObjects;
			}
			else
			{
				// Check to make sure nothing selected is already in a Sector.
				Transform parent = gameObject.transform;
				while(parent != null)
				{
					// Bail out if we encounter any classes that shouldn't be part of the list
					if(parent.GetComponent<SECTR_Sector>() != null)
					{
						EditorUtility.DisplayDialog("Sector Safety First", "Some of the selected objects are already in a Sector. Please unparent them from the current Sector before putting them into a new Sector.", "Ok");
                        //refresh the lock timestamp since the user needs some time to read & confirm the dialogue
                        nextContextMenuExecutionAllowedTimeStamp = SECTR_SectorUtils.GetUnixTimeStamp() + 1000;
                        return;
					}
					parent = parent.parent;
				}
				++selectedObjectIndex;
			}
		}

		// Build a list of common parents from the selection to try to preserve any existing heirarchy
		// but without walking all the way up the tree. Complex scenes may have many Sectors parented
		// under a single transform and we do not want to walk higher than the current selection.
		for(selectedObjectIndex = 0; selectedObjectIndex < numSelectedObjects; ++selectedObjectIndex )
		{
			GameObject gameObject = selectedObjects[selectedObjectIndex];
			Transform parent = gameObject.transform;
			while(parent != null)
			{
				if(parent.parent)
				{
					int numChildren = parent.parent.childCount;
					bool allChildrenSelected = true;
					for(int childIndex = 0; childIndex < numChildren; ++childIndex)
					{
						if(!selectedObjects.Contains(parent.parent.GetChild(childIndex).gameObject))
						{
							allChildrenSelected = false;
							break;
						}
					}
					if(allChildrenSelected)
					{
						parent = parent.parent;
					}
					else
					{
						break;
					}
				}
				else
				{
					break;
				}
			}
			if(parent != null && !rootObjects.Contains(parent.gameObject))
			{
				rootObjects.Add(parent.gameObject);
			}
		}

		int numRootObjects = rootObjects.Count;
		string newName = "SECTR Sector";
		string undoName = "Create " + newName;
		SECTR_Sector newSector = null;

		// If there is just one root, give the open to make that into a Sector. This helps
		// with scenes that are already well organized.
		Transform commonParent = numRootObjects == 1 ? rootObjects[0].transform : null;
		if(commonParent && EditorUtility.DisplayDialog("Common Parent Detected", "Selected objects are all a child of " + commonParent.name + ". \n" +
			"Would you like to make " + commonParent.name + " into a Sector? \n\n "+ 
            "If yes, a Sector component will be added to " + commonParent.name + ".\n\n" + 
            "If not, a new Game Object will be created as new Sector and " +
		    commonParent.name + " will be the first child of it.", "Yes", "No"))
		{
			newSector = commonParent.gameObject.AddComponent<SECTR_Sector>();
			if(!commonParent.gameObject.isStatic &&
			   EditorUtility.DisplayDialog("Make Sector Static?", "SECTR can perform additional optimizations on Sectors that are marked as static, provided they " +
			   	"do not need to move or change bounds at runtime. Would you like " + commonParent.name + " to be marked as static?", "Yes", "No"))
			{
				commonParent.gameObject.isStatic = true;
			}
			SECTR_Undo.Created(newSector, undoName);
		}
		else
		{
			commonParent = numRootObjects > 0 ? rootObjects[0].transform.parent : null;
			for(int rootIndex = 0; rootIndex < numRootObjects; ++rootIndex)
			{
				if(rootObjects[rootIndex].transform.parent != commonParent)
				{
					commonParent = null;
					break;
				}
			}

			GameObject newGameObject = CreateGameObject(newName);
			newGameObject.transform.parent = commonParent;
			newGameObject.isStatic = true;
			SECTR_Undo.Created(newGameObject, undoName);

			List<Vector3> rootPositions = new List<Vector3>(numRootObjects);
			for(int rootObjectIndex = 0; rootObjectIndex < numRootObjects; ++rootObjectIndex)
			{
				GameObject gameObject = rootObjects[rootObjectIndex];
				rootPositions.Add(gameObject.transform.position);
				SECTR_Undo.Parent(newGameObject, gameObject, undoName);
			}

			newSector = newGameObject.AddComponent<SECTR_Sector>();
			SECTR_Undo.Created(newSector, undoName);

			newSector.transform.position = newSector.TotalBounds.center;
			for(int rootObjectIndex = 0; rootObjectIndex < numRootObjects; ++rootObjectIndex )
			{
				GameObject gameObject = rootObjects[rootObjectIndex];
				gameObject.transform.position = rootPositions[rootObjectIndex];
			}
		}

		List<SECTR_Member.Child> sharedChildren = newSector.GetSharedChildren();
		if(sharedChildren.Count > 0 && EditorUtility.DisplayDialog("Overlap Warning", "Some objects in this Sector overlap other Sectors, which may cause unexpected behavior. Would you like to make them Members instead of children?", "Yes", "No"))
		{
			SECTR_SectorEditor.MakeSharedChildrenMembers(newSector, sharedChildren, undoName);
		}

		Selection.activeGameObject = newSector.gameObject;
	}

	[MenuItem(createPortalItem, false, createPortalPriority)]
	public static void CreatePortal()
	{
		string newName = "SECTR Portal";
		string undoName = "Create " + newName;
		GameObject newGameObject = CreateGameObject(newName);
		SECTR_Portal newPortal = newGameObject.AddComponent<SECTR_Portal>();
		newPortal.ForceEditHull = true;
		newPortal.CenterOnEdit = true;
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createMemberItem, false, createMemberPriority)]
	public static void CreateMember()
	{
		string newName = "SECTR Member";
		string undoName = "Create " + newName;
		GameObject newGameObject = CreateGameObject(newName);
		newGameObject.AddComponent<SECTR_Member>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createDoorItem, false, createDoorPriority)]
	public static void CreateCoreDoor()
	{
		CreateDoor<SECTR_Door>("SECTR Core Door");
	}

	//[MenuItem(introWindowItem, false, introWindowPriority)]
	//public static void OpenIntroWindow()
	//{
	//	// Get existing open window or if none, make a new one:
	//	SECTR_IntroWindow.ShowWindow();
	//}

	[MenuItem(terrainWindowItem, false, terrainWindowPriority)]
	public static void OpenTerrainWindow()
	{
		// Get existing open window or if none, make a new one:		
		SECTR_TerrainWindow window = EditorWindow.GetWindow<SECTR_TerrainWindow>("SECTR Terrain");
		window.Show();
	}
}