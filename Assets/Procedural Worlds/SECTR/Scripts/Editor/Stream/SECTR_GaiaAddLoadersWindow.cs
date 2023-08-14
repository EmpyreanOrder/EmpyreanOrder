#if GAIA_PRESENT && UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SECTR_GaiaAddLoadersWindow : SECTR_Window
{
	private readonly GUIContent START_LOADER_BTN_LABEL = new GUIContent(
		"Start Loader (Optional)",
		"Useful loader to have a fade in effect at startup, or to be used in combination " +
		"with Loading Door, to balance out the door's reference counts. It loads whatever " +
		"Sectors it is in at Start, and then removes itself from the game. " +
		"You will also need one of the below.");
	private readonly GUIContent REGION_LOADER_BTN_LABEL = new GUIContent(
		"Region Loader (Recommended)",
		"This is your go to loader for typical outdoors streaming. You can specify " +
		"3 dimensions of a loading box around the camera, that will trigger " +
		"loading when hitting sectors.");
	private readonly GUIContent NEIGHBOR_LOADER_BTN_LABEL = new GUIContent(
		"Neighbor Loader",
		"This will load neighbors of the current sector the camera is in. You " +
		"can specify how deep this neighbor grabbing should go.");

	private GUIStyle m_btnPanelStyle;
	private string m_msg;

	private void OnEnable()
	{
		this.minSize = this.maxSize = new Vector2(450, 270);
	}

	#region Unity Interface
	protected override void OnGUI()
	{
		base.OnGUI();

		if (m_btnPanelStyle == null)
		{
			m_btnPanelStyle = new GUIStyle()
			{
				margin = new RectOffset(10, 10, 10, 10),
			};
		}

		GUILayout.Label("Add Loaders", EditorStyles.boldLabel);
		GUILayout.Label("Select Loaders to add to your controller.\n\n" +
			"Note: Start loader does loading at the start of the scene and is not sufficient\n" +
			"on its own. If you add a Start Loader, you will need a second one as well.");

		GUILayout.BeginVertical(m_btnPanelStyle);
		{
			LoaderSelectionMenu();
		}
		GUILayout.EndVertical();

		GUILayout.FlexibleSpace();
		if (m_msg != null)
		{
			EditorGUILayout.HelpBox(m_msg, MessageType.Info);
		}
	}
	#endregion

	#region Helper methods

	private void LoaderSelectionMenu()
	{
		if (GUILayout.Button(START_LOADER_BTN_LABEL))
		{
			m_msg = AddLoaderToCamObject<SECTR_StartLoader>();
		}

		EditorGUILayout.Space();
		if (GUILayout.Button(REGION_LOADER_BTN_LABEL))
		{
			m_msg = AddLoaderToCamObject<SECTR_RegionLoader>();
		}

		EditorGUILayout.Space();
		if (GUILayout.Button(NEIGHBOR_LOADER_BTN_LABEL))
		{
			m_msg = AddLoaderToCamObject<SECTR_NeighborLoader>();
		}
	}
	
	private string AddLoaderToCamObject<T>() where T : SECTR_Loader
	{
		Camera camera = Camera.main;
		if (camera == null)
		{
			camera = FindObjectOfType<Camera>();
		}
		if (camera == null)
		{
			EditorUtility.DisplayDialog("OOPS!", "Could not find the controller with a camera to add loaders to. Please add a controller/camera to your scene.", "OK");
			return null;
		}

		GameObject go = camera.gameObject;
		if (go == null)
		{
			EditorUtility.DisplayDialog("OOPS!", "Found a camera but it doesn't seem to belong to a game object. Please add a controller/camera to your scene.", "OK");
			return null;
		}

		Selection.activeGameObject = go;

		string msg;
		Component component = go.GetComponent<T>();
		if (component != null)
		{
			msg = "Game object '" + go.name + "' already has " + component.ToString() + " component(s).";
		} else {
			component = go.AddComponent<T>();
			msg = component.ToString() + " was added to '" + go.name + "'.";
		}


        if (typeof(T) == typeof(SECTR_RegionLoader))
        {
            ((SECTR_RegionLoader)component).LoadSize = new Vector3(200f, 200f, 200f);
        }

        if (typeof(T) != typeof(SECTR_StartLoader))
		{
			return msg;
		}		

		SECTR_StartLoader loaderComponent = go.GetComponent<SECTR_StartLoader>();
		if (loaderComponent == null)
		{
			return msg + " Unable to enable Fade In for " + component.ToString() + ".";
		}

		loaderComponent.FadeIn = true;
		return msg + " Enabled Fade In for " + component.ToString() + ".";
	}
	#endregion
}

#endif
