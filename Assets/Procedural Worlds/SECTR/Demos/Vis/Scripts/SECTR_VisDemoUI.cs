// Copyright (c) 2014 Make Code Now! LLC
using UnityEngine;
using System.Collections;

//[AddComponentMenu("SECTR/Demos/SECTR Vis Demo UI")]
public class SECTR_VisDemoUI : SECTR_DemoUI
{
	#region Private Details
	private string originalDemoMessage;
	#endregion

	#region Public Interface
	[Multiline] public string Unity4PerfMessage;
	#endregion

	#region Unity Interface
	void Start()
	{
		if(PipController && PipController.GetComponent<SECTR_CullingCamera>() == null &&
		   GetComponent<SECTR_CullingCamera>() && GetComponent<Camera>())
		{
			SECTR_CullingCamera proxyCamera = PipController.gameObject.AddComponent<SECTR_CullingCamera>();
			proxyCamera.cullingProxy = GetComponent<Camera>();
		}
	}

	protected override void OnEnable()
	{
		originalDemoMessage = DemoMessage;
		watermarkLocation = WatermarkLocation.UpperCenter;
		AddButton(KeyCode.C, "Enable Culling", "Disable Culling", ToggleCulling);
		base.OnEnable();
	}

	protected override void OnGUI()
	{
		if(Application.isEditor && Application.isPlaying && !string.IsNullOrEmpty(Unity4PerfMessage))
		{
			DemoMessage = originalDemoMessage;
			DemoMessage += "\n\n";
			DemoMessage += Unity4PerfMessage;
		}

		base.OnGUI();

		if(passedIntro && !CaptureMode)
		{
			int renderersCulled = 0;
			int lightsCulled = 0;
			int terrainsCulled = 0;

			SECTR_CullingCamera cullingCamera = GetComponent<SECTR_CullingCamera>();
			if(cullingCamera)
			{
				renderersCulled += cullingCamera.RenderersCulled;
				lightsCulled += cullingCamera.LightsCulled;
				terrainsCulled += cullingCamera.TerrainsCulled;
			}

			string statsString = "Culling Stats\n";
			statsString += "Renderers: " + renderersCulled + "\n";
			statsString += "Lights: " + lightsCulled + "\n";
			statsString += "Terrains: " + terrainsCulled;

			GUIContent statsContent = new GUIContent(statsString);
			float width = Screen.width * 0.33f;
			float height = demoButtonStyle.CalcHeight(statsContent, width);
			Rect statsRect = new Rect(Screen.width - width, 0, width, height);
			GUI.Box(statsRect, statsContent, demoButtonStyle);
		}
	}
	#endregion

	#region Private Details
	protected void ToggleCulling(bool active)
	{
		SECTR_CullingCamera cullingCamera = GetComponent<SECTR_CullingCamera>();
		if(cullingCamera)
		{
			cullingCamera.enabled = !active;
			cullingCamera.ResetStats();
		}
	}
	#endregion
}
