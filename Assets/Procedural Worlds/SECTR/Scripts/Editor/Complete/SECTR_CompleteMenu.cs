using UnityEngine;
using UnityEditor;
using System.Collections;

public class SECTR_CompleteMenu : SECTR_Menu 
{
	const string rootCreatePath = createMenuRootPath + "COMPLETE/";
	const string createDoorItem = rootCreatePath + "Complete Door";
	const int createDoorPriority = completePriority + 0;

	[MenuItem(createDoorItem, false, createDoorPriority)]
	public static void CreateCompleteDoor()
	{
		GameObject newDoor = CreateDoor<SECTR_LoadingDoor>("SECTR Complete Door");
		newDoor.AddComponent<SECTR_DoorAudio>();
	}
}
