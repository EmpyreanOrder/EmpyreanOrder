// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SECTR_LOD))]
[CanEditMultipleObjects]
public class SECTR_LODEditor : SECTR_Editor
{
	#region Private Details
	private Dictionary<Transform, bool> hierarchyFoldouts = new Dictionary<Transform, bool>();
	private GUIStyle lodButtonStyle = null;
	bool detailsFoldout = true;
	private int selectedLOD = -1;
	private bool dragging = false;
	private List<Rect> lodRects = new List<Rect>();
	private Rect lodListRectPerm = new Rect();
	#endregion

	public override void OnDisable()
	{
		SECTR_LOD myLOD = (SECTR_LOD)target;
		myLOD.Reset();
        base.OnDisable();
	}
	
	public override void OnInspectorGUI()
	{
		SECTR_LOD myLOD = (SECTR_LOD)target;
		float lodButtonHeight = 50;
		float lodGutterSize = 4f;

		if(lodButtonStyle == null)
		{
			lodButtonStyle = new GUIStyle(GUI.skin.button);
			lodButtonStyle.padding = new RectOffset();
			lodButtonStyle.margin = new RectOffset();
		}

		if(Event.current.type == EventType.Repaint)
		{
			lodRects.Clear();
		}

		EditorGUILayout.BeginVertical();

		Rect lodListRect = EditorGUILayout.BeginVertical();
		GUI.enabled = false;
		GUILayout.Button(GUIContent.none, GUILayout.Height(lodButtonHeight));
		GUI.enabled = true;
		EditorGUILayout.EndVertical();

		float addButtonSize = lodListRect.width * 0.1f;
		int numLODs = myLOD.LODs.Count;
		float minThreshold = 1;
		float insertPos = lodListRect.xMin;
		for(int lodIndex = 0; lodIndex < numLODs; ++lodIndex)
		{
			SECTR_LOD.LODSet thisSet = myLOD.LODs[lodIndex];
			float buttonScale = 1f - thisSet.Threshold;
			float percent = 1f;
			if(lodIndex > 0)
			{
				float prevThreshold = myLOD.LODs[lodIndex - 1].Threshold;
				buttonScale -= 1f - prevThreshold;
				percent = myLOD.LODs[lodIndex - 1].Threshold;
			}

			float buttonWidth = buttonScale * (lodListRect.width - addButtonSize);
			Rect buttonRect = new Rect(insertPos + lodGutterSize * 0.5f, lodListRect.y, buttonWidth - lodGutterSize, lodListRect.height);

			if(GUI.Button(buttonRect, "LOD" + lodIndex + "\n" + percent.ToString("P1"), lodButtonStyle))
			{
				selectedLOD = lodIndex;
			}
			if(Event.current.type == EventType.Repaint)
			{
				lodRects.Add(buttonRect);
			}
			insertPos += buttonWidth;

			minThreshold = Mathf.Min(minThreshold, thisSet.Threshold);
		}

		if(minThreshold > 0f && myLOD.LODs.Count > 0)
		{
			Rect culledRect = new Rect(insertPos + lodGutterSize * 0.5f, lodListRect.y, lodListRect.xMax - insertPos - addButtonSize - lodGutterSize, lodListRect.height);
			if(GUI.Button(culledRect, "Culled\n" + minThreshold.ToString("P1"), lodButtonStyle))
			{
				selectedLOD = -1;
			}

			if(Event.current.type == EventType.Repaint)
			{
				lodRects.Add(culledRect);
			}
		}

		GUI.enabled = minThreshold > 0;
		Rect addRect = myLOD.LODs.Count > 0 ? new Rect(lodListRect.xMax - addButtonSize, lodListRect.y, addButtonSize, lodListRect.height) :
											  new Rect(lodListRect.xMin, lodListRect.y, lodListRect.width, lodListRect.height);
		if(GUI.Button(addRect, myLOD.LODs.Count > 0 ? "+" : "+\n(Add LOD)", lodButtonStyle))
		{
			SECTR_Undo.Record(myLOD, "Added LOD");
			SECTR_LOD.LODSet newSet = new SECTR_LOD.LODSet();
			if(myLOD.LODs.Count == 0)
			{
				Transform[] children = myLOD.GetComponentsInChildren<Transform>();
				int numChildren = children.Length;
				for(int childIndex = 0; childIndex < numChildren; ++childIndex)
				{
					if(children[childIndex] != myLOD.transform)
					{
						newSet.Add(children[childIndex].gameObject, null);
					}
				}
			}
			else
			{
				SECTR_LOD.LODSet prevSet = myLOD.LODs[myLOD.LODs.Count - 1];
				int numEntries = prevSet.LODEntries.Count;
				for(int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
				{
					SECTR_LOD.LODEntry prevEntry = prevSet.LODEntries[entryIndex];
					newSet.Add(prevEntry.gameObject, prevEntry.lightmapSource);
				}
			}
			newSet.Threshold = minThreshold * 0.5f;
			selectedLOD = myLOD.LODs.Count;
			myLOD.LODs.Add(newSet);
		}
		GUI.enabled = true;
		
		int lodToRemove = -1;
		if(selectedLOD >= 0 && selectedLOD < myLOD.LODs.Count)
		{
			float sliderMin = 0;
			float sliderMax = 1;

			if(selectedLOD < myLOD.LODs.Count - 1)
			{
				sliderMin = myLOD.LODs[selectedLOD + 1].Threshold;
			}
			if(selectedLOD > 0)
			{
				sliderMax = myLOD.LODs[selectedLOD - 1].Threshold;
			}

			myLOD.LODs[selectedLOD].Threshold = EditorGUILayout.Slider("LOD" + selectedLOD + " Threshold", myLOD.LODs[selectedLOD].Threshold, sliderMin, sliderMax);
		
			detailsFoldout = EditorGUILayout.Foldout(detailsFoldout, "LOD" + selectedLOD + " Members");
			if(detailsFoldout)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Name");
				GUILayout.Label("Included");
				GUILayout.Label("Lightmap Proxy");
				EditorGUILayout.EndHorizontal();
				_BuildChildControls(myLOD, myLOD.LODs[selectedLOD], myLOD.transform, true);
			}

			if(GUILayout.Button("Remove LOD" + selectedLOD))
			{
				if(EditorUtility.DisplayDialog("Confirm LOD Removal", "Are you sure you wish to remove LOD " + selectedLOD + "?", "Yes", "No"))
				{
					lodToRemove = selectedLOD;
				}
			}
		}
		else if(selectedLOD == -1)
		{
			serializedObject.Update();
			DrawProperty("CullSiblings");
			serializedObject.ApplyModifiedProperties();
		}

		EditorGUILayout.EndVertical();

		if(lodToRemove >= 0)
		{
			SECTR_Undo.Record(myLOD, "Removed LOD");
			myLOD.LODs.RemoveAt(lodToRemove);
		}

		if(Event.current.type == EventType.Repaint)
		{
			lodListRectPerm = lodListRect;
		}

		int dragLod = -1;
		if(lodListRectPerm.Contains(Event.current.mousePosition))
		{
			for(int lodIndex = 0; lodIndex < lodRects.Count - 1; ++lodIndex)
			{
				if(Event.current.mousePosition.x > lodRects[lodIndex].xMax &&
				   Event.current.mousePosition.x < lodRects[lodIndex + 1].xMin)
				{
					dragLod = lodIndex;
					break;
				}
			}
		}

		if(dragLod >= 0 && Event.current.type == EventType.MouseDown)
		{
			dragging = true;
			selectedLOD = dragLod;
		}
		else if(Event.current.type == EventType.MouseUp)
		{
			dragging = false;
		}

		if(dragging && lodListRectPerm.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDrag)
		{
			float newThreshold = 1f - ((Event.current.mousePosition.x - lodListRect.x) / lodListRect.width);
			newThreshold = Mathf.Clamp01(newThreshold);
			if(selectedLOD < myLOD.LODs.Count - 1)
			{
				newThreshold = Mathf.Max(newThreshold, myLOD.LODs[selectedLOD + 1].Threshold);
			}
			if(selectedLOD > 0)
			{
				newThreshold = Mathf.Min(newThreshold, myLOD.LODs[selectedLOD - 1].Threshold);
			}
			myLOD.LODs[selectedLOD].Threshold = newThreshold;
		}

		if(dragging || dragLod >= 0)
		{
			EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 16, 
			                                        Event.current.mousePosition.y - 16, 32, 32), 
			                               			MouseCursor.ResizeHorizontal);
		}
	}

	private void OnSceneGUI()
	{
		if(!EditorApplication.isPlaying)
		{
			SECTR_LOD myLOD = (SECTR_LOD)target;
			myLOD.SelectLOD(SceneView.lastActiveSceneView.camera);
		}
	}

	private void _BuildChildControls(SECTR_LOD myLOD, SECTR_LOD.LODSet lodSet, Transform transform, bool rootTransform)
	{
		if(transform)
		{
			if(!rootTransform)
			{
				++EditorGUI.indentLevel;
			}
			int numChildren = transform.childCount;
			bool expanded = false;
			if(!rootTransform)
			{
				GUI.enabled = transform.parent == myLOD.transform || lodSet.GetEntry(transform.parent.gameObject) != null ;
				expanded = _DrawChildControl(myLOD, lodSet, transform, numChildren > 0);
				GUI.enabled = true;
			}
			if(expanded || rootTransform)
			{
				for(int childIndex = 0; childIndex < numChildren; ++childIndex)
				{
					_BuildChildControls(myLOD, lodSet, transform.GetChild(childIndex), false);
				}
			}
		}
		if(!rootTransform)
		{
			--EditorGUI.indentLevel;
		}
	}

	private bool _DrawChildControl(SECTR_LOD myLOD, SECTR_LOD.LODSet lodSet, Transform transform, bool hasChildren)
	{
		string undoString = "Changed LOD";
		bool expanded = false;
		float labelWidth = Screen.width * 0.3f;
		float checkWidth = 30;
		float buffer = 5;
		Rect propertyRect = EditorGUILayout.BeginHorizontal(GUILayout.Width(labelWidth));
		if(hasChildren)
		{
			hierarchyFoldouts.TryGetValue(transform, out expanded);
			hierarchyFoldouts[transform] = EditorGUILayout.Foldout(expanded, transform.name);
		}
		else
		{
			EditorGUILayout.LabelField(transform.name, GUILayout.Width(labelWidth));
		}
		EditorGUILayout.EndHorizontal();
		
		SECTR_LOD.LODEntry entry = lodSet.GetEntry(transform.gameObject);
		bool isChecked = entry != null;
		bool newChecked = GUI.Toggle(new Rect(propertyRect.xMax + buffer, 
		                                      propertyRect.y, 
		                                      checkWidth, 
		                                      propertyRect.height), 
		                             isChecked, 
		                             GUIContent.none);
		
		if(newChecked != isChecked)
		{
			SECTR_Undo.Record(myLOD, undoString);
			if(newChecked)
			{
				entry = lodSet.Add(transform.gameObject, null);
			}
			else
			{
				lodSet.Remove(transform.gameObject);
				entry = null;
			}
			isChecked = newChecked;
			myLOD.Reset();
		}

		if(entry != null && transform.GetComponent<Renderer>())
		{
			Renderer newSource = (Renderer)EditorGUI.ObjectField(new Rect(propertyRect.xMax + checkWidth + buffer,
			                                                              propertyRect.y,
			                                                              Screen.width - (propertyRect.xMax + checkWidth + buffer * 2),
			                                                              propertyRect.height),
			                                                     GUIContent.none, 
			                                                     entry.lightmapSource, 
			                                                     typeof(Renderer), 
			                                                     true);
			if(newSource != entry.lightmapSource)
			{
				SECTR_Undo.Record(myLOD, undoString);
				entry.lightmapSource = newSource;
				myLOD.Reset();
			}
		}
		return expanded;
	}
}
