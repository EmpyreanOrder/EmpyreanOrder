// Copyright (c) 2014 Make Code Now! LLC

using UnityEditor;
using UnityEngine;

public class SECTR_AudioMenu : SECTR_Menu
{
	const string rootCreatePath = createMenuRootPath + "AUDIO/";
	const string rootAssetPath = assetMenuRootPath + "AUDIO/";
	const string createSystemItem = rootCreatePath + "Audio System";
	const string createPointItem = rootCreatePath + "Point Source";
	const string createRegionItem = rootCreatePath + "Region Source";
	const string createSplineItem = rootCreatePath + "Spline Source";
	const string createPropagationItem = rootCreatePath + "Propagation Source";
	const string createTriggerItem = rootCreatePath + "Trigger Source";
	const string createEnvTriggerItem = rootCreatePath + "Audio Environment Trigger";
	const string createZoneItem = rootCreatePath + "Audio Environment Zone";
	const string createStartItem = rootCreatePath + "Start Music";
	const string createMusicItem = rootCreatePath + "Music Trigger";
	const string createDoorItem = rootCreatePath + "Audio Door";
	const string createImpactItem = rootCreatePath + "Impact Audio Cube";
	const string createBusItem = rootAssetPath + "Bus";
	const string createCueItem = rootAssetPath + "Cue";
	const int createSystemPriority = audioPriority + 0;
	const int createPointPriority = audioPriority + 50;
	const int createRegionPriority = audioPriority + 55;
	const int createSplinePriority = audioPriority + 60;
	const int createPropagationPriority = audioPriority + 65;
	const int createTriggerPriority = audioPriority + 75;
	const int createEnvTriggerPriority = audioPriority + 100;
	const int createZonePriority = audioPriority + 105;
	const int createStartPriority = audioPriority + 150;
	const int createMusicPriority = audioPriority + 155;
	const int createDoorPriority = audioPriority + 200;
	const int createImpactPriority = audioPriority + 205;

	const string editProjectItem = windowMenuRootPath + "Audio Window";
	const int editProjectPriority = windowPriority;

	[MenuItem(createSystemItem, false, createSystemPriority)]
	public static void CreateAudioSystem()
	{
		string newObjectName = "SECTR Audio System";
		string undoName = "Created " + newObjectName;
		AudioListener[] listeners = (AudioListener[])FindObjectsOfType(typeof(AudioListener));
		int numListeners = listeners.Length;
		if(listeners.Length > 0)
		{
			for(int listenerIndex = 0; listenerIndex < numListeners; ++listenerIndex)
			{
				AudioListener listener = listeners[listenerIndex];
				if(listener.GetComponent<SECTR_AudioSystem>())
				{
					Debug.LogWarning("Scene already has an Audio System. Can't create any more.");
					Selection.activeGameObject = listener.gameObject;
					return;
				}
			}

			SECTR_AudioSystem newSystem = listeners[0].gameObject.AddComponent<SECTR_AudioSystem>();
			SECTR_Undo.Created(newSystem, undoName);
			Selection.activeGameObject = newSystem.gameObject;
		}
		else
		{
			Debug.LogWarning("Couldn't find an audio listener. This could be fine or could indicate a problem with your scene.");
			GameObject newGameObject = CreateGameObject(newObjectName);
			newGameObject.AddComponent<SECTR_AudioSystem>();
			SECTR_Undo.Created(newGameObject, undoName);
			Selection.activeGameObject = newGameObject;
		}
	}

	[MenuItem(createPointItem, false, createPointPriority)]
	public static void CreatePointSource()
	{
		string newObjectName = "SECTR Point Source";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		newGameObject.AddComponent<SECTR_PointSource>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createRegionItem, false, createRegionPriority)]
	public static void CreateRegionSource()
	{
		string newObjectName = "SECTR Region Source";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		BoxCollider newCollider = newGameObject.AddComponent<BoxCollider>();
		newCollider.isTrigger = true;
		newGameObject.AddComponent<SECTR_RegionSource>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createSplineItem, false, createSplinePriority)]
	public static void CreateSplineSource()
	{
		string newObjectName = "SECTR Spline Source";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		SECTR_SplineSource newSpline = newGameObject.AddComponent<SECTR_SplineSource>();
		int numInitialKeys = 3;
		float keySpread = 10;
		float deltaX = keySpread / (numInitialKeys - 1);
		for(int keyIndex = 0; keyIndex < numInitialKeys; ++keyIndex)
		{
			GameObject newKey = new GameObject(newObjectName + " Key" + keyIndex);
			newKey.transform.parent = newGameObject.transform;
			newKey.transform.localPosition = new Vector3((keySpread * -0.5f) + (keyIndex * deltaX), 0, 0);
			newSpline.SplinePoints.Add(newKey.transform);
			SECTR_Undo.Created(newKey, undoName);
		}
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createPropagationItem, false, createPropagationPriority)]
	public static void CreatePropagationSource()
	{
		string newObjectName = "SECTR Propagation Source";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		newGameObject.AddComponent<SECTR_PropagationSource>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createImpactItem, false, createImpactPriority)]
	public static void CreateImpactSource()
	{
		string newObjectName = "SECTR Impact Audio Cube";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		newGameObject.name = newObjectName;
		newGameObject.AddComponent<Rigidbody>();
		newGameObject.AddComponent<SECTR_ImpactAudio>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createTriggerItem, false, createTriggerPriority)]
	public static void CreateTriggerSource()
	{
		string newObjectName = "SECTR Trigger Source";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		BoxCollider newCollider = newGameObject.AddComponent<BoxCollider>();
		newCollider.isTrigger = true;
		newGameObject.AddComponent<SECTR_TriggerSource>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createEnvTriggerItem, false, createEnvTriggerPriority)]
	public static void CreateAudioEnvironmentTrigger()
	{
		string newObjectName = "SECTR Audio Environment Trigger";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		BoxCollider newCollider = newGameObject.AddComponent<BoxCollider>();
		newCollider.isTrigger = true;
		newGameObject.AddComponent<SECTR_AudioEnvironmentTrigger>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createZoneItem, false, createZonePriority)]
	public static void CreateAudioEnvironmentZone()
	{
		string newObjectName = "SECTR Audio Environment Zone";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		newGameObject.AddComponent<SECTR_AudioEnvironmentZone>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createStartItem, false, createStartPriority)]
	public static void CreateStartMusic()
	{
		string newObjectName = "SECTR Start Music";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		newGameObject.AddComponent<SECTR_StartMusic>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createMusicItem, false, createMusicPriority)]
	public static void CreateMusicTrigger()
	{
		string newObjectName = "SECTR Music Trigger";
		string undoName = "Created " + newObjectName;
		GameObject newGameObject = CreateGameObject(newObjectName);
		BoxCollider newCollider = newGameObject.AddComponent<BoxCollider>();
		newCollider.isTrigger = true;
		newGameObject.AddComponent<SECTR_MusicTrigger>();
		SECTR_Undo.Created(newGameObject, undoName);
		Selection.activeGameObject = newGameObject;
	}

	[MenuItem(createDoorItem, false, createDoorPriority)]
	public static void CreateAudioDoor()
	{
		GameObject newDoor = CreateDoor<SECTR_Door>("SECTR Audio Door");
		newDoor.AddComponent<SECTR_DoorAudio>();
	}

	[MenuItem(createBusItem, false, assetPriority)]
	public static void CreateAudioBus()
	{
		string assetPath = null;
		SECTR_Asset.Create<SECTR_AudioBus>(null, "New SECTR Audio Bus", ref assetPath);
	}

	[MenuItem(createCueItem, false, assetPriority)]
	public static void CreateAudioCue()
	{
		string assetPath = null;
		SECTR_Asset.Create<SECTR_AudioCue>(null, "New SECTR Audio Cue", ref assetPath);
	}

	[MenuItem(editProjectItem, false, editProjectPriority)]
	public static void EditProject()
	{
		// Get existing open window or if none, make a new one:		
		SECTR_AudioWindow window = EditorWindow.GetWindow<SECTR_AudioWindow>("SECTR Audio");
		window.Show();
	}
}