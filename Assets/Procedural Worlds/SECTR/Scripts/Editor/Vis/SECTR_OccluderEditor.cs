// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_Occluder))]
[CanEditMultipleObjects]
public class SECTR_OccluderEditor : SECTR_HullEditor
{
	#region Members
	private GUIStyle boxStyle = null;
	private GUIStyle buttonStyle = null;
	#endregion

	#region Unity Interface
	public void OnSceneGUI() 
	{
		SECTR_Occluder myOccluder = (SECTR_Occluder)target;

		if(boxStyle == null)
		{
			boxStyle = new GUIStyle(GUI.skin.box);
			boxStyle.alignment = TextAnchor.UpperCenter;
			boxStyle.fontSize = 15;
			boxStyle.normal.textColor = Color.white;
		}
		
		if(buttonStyle == null)
		{
			buttonStyle = new GUIStyle(GUI.skin.button);
			buttonStyle.alignment = TextAnchor.UpperCenter;
			buttonStyle.fontSize = 12;
			buttonStyle.normal.textColor = Color.white;
		}
		
		// Viewport GUI Drawing
		_DrawViewportGUI(myOccluder);

		if((createHull || myOccluder.ForceEditHull) && !Application.isPlaying)
		{
			_EditHull(myOccluder);
		}
		
		// Input may destroy this object.
		if(target == null)
		{
			return;
		}
		
		if(createHull || myOccluder.ForceEditHull)
		{
			_DrawHullEditor(myOccluder);
		}
	}
	#endregion

	#region Private Methods
	void _DrawViewportGUI(SECTR_Occluder myOccluder)
	{
		Handles.BeginGUI();
		int width = 500;
		if(createHull || myOccluder.ForceEditHull)
		{
			float height = 100;
			string returnText = "";
			if(newHullVerts.Count >= 3)
			{
				returnText = "Return to complete.";
			}
			else if(newHullVerts.Count == 0 && myOccluder.ForceEditHull)
			{
				returnText = "Return to create empty occluder.";
			}
			GUI.Box(new Rect((Screen.width * 0.5f) - (width * 0.5f), Screen.height - height, width, height),
			        "Drawing geometry for " + myOccluder.name + ".\n" + 
			        (closesetVertIsValid ? "Left Click to add vert. " : "") + returnText + "\nEsc to cancel.",
			        boxStyle);
		}
		else if(Selection.gameObjects.Length <= 1)
		{
			float height = 20;
			if(GUI.Button(new Rect((Screen.width * 0.5f) - (width * 0.5f), Screen.height - (height * 4f), width, height), 
			              new GUIContent(myOccluder.HullMesh ? "Redraw Occluder" : "Draw Occluder", "Allows you to (re) draw the geometry of this Occluder."), 
			              buttonStyle))
			{
				createHull = true;
			}
		}
		Handles.EndGUI();
	}
	#endregion
}
