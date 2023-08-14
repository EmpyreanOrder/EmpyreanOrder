// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System;

[CustomEditor(typeof(SECTR_AudioCue))]
[CanEditMultipleObjects]
public class SECTR_AudioCueEditor : SECTR_Editor
{
	#region Private Details
	private bool basicFoldout = true;
	private bool advancedFoldout = true;
	private bool parameterFoldout = true;
	private bool clipFoldout = true;
	private bool spatializationFoldout = true;
	private Texture playIcon = null;
	private Texture removeIcon = null;
	private GUIStyle clipStyle = null;
	private GUIStyle iconButtonStyle = null;
	private SECTR_ComputeRMS bakeMaster = null;
	private SerializedProperty busProp;
	private SerializedProperty templateProp;
	private AnimationCurve dummyCurve = new AnimationCurve();
	#endregion

	#region Public Interface
	public bool DrawBus = true;
	#endregion

	#region Unity Interface
	public override void OnEnable()
	{
		base.OnEnable();
		playIcon = SECTR_AudioWindow.LoadIcon("PlayIcon.psd");
		removeIcon = SECTR_AudioWindow.LoadIcon("RemoveClipIcon.psd");
		busProp = serializedObject.FindProperty("bus");
		templateProp = serializedObject.FindProperty("template");
	}

	public override void OnInspectorGUI()
	{
		SECTR_AudioCue myCue = (SECTR_AudioCue)target;

		if(clipStyle == null)
		{
			clipStyle = new GUIStyle(GUI.skin.label);
			clipStyle.alignment = TextAnchor.MiddleCenter;
		}

		if(iconButtonStyle == null)
		{
			iconButtonStyle = new GUIStyle(GUI.skin.button);
			iconButtonStyle.padding = new RectOffset(2,2,2,2);
			iconButtonStyle.imagePosition = ImagePosition.ImageOnly;
		}

		serializedObject.Update();
		DrawProperties(myCue);
		serializedObject.ApplyModifiedProperties();
	}
	#endregion

	#region Private Methods
	void DrawProperties(SECTR_AudioCue myCue)
	{
		bool wasEnabled = GUI.enabled;
		bool multiSelect = targets.Length > 1;

		bool drawTemplate = !multiSelect || !myCue.IsTemplate;
		bool draw2D3D = true;
		if(multiSelect)
		{
			for(int targetIndex = 1; targetIndex < targets.Length; ++targetIndex)
			{
				SECTR_AudioCue targetCue = (SECTR_AudioCue)(targets[targetIndex]);
				if(targetCue.Spatialization != myCue.Spatialization)
				{
					draw2D3D = false;
				}
				if(targetCue.IsTemplate != myCue.IsTemplate)
				{
					drawTemplate = false;
				}
			}
		}

		if(DrawBus)
		{
			SECTR_AudioBus oldBus = myCue.Bus;
			GUI.SetNextControlName(myCue + "_Bus");
			SECTR_AudioBus newBus = ObjectField<SECTR_AudioBus>("Bus", "Mixing Bus for this Cue.", oldBus, false);
			if(oldBus != newBus)
			{
				if(oldBus)
				{
					oldBus.RemoveCue(myCue);
				}
				busProp.objectReferenceValue = newBus;
				if(newBus)
				{
					newBus.AddCue(myCue);
				}
			}
		}

		if(drawTemplate)
		{
			if(!myCue.IsTemplate)
			{
				SECTR_AudioCue oldTemplate = myCue.Template;
				GUI.SetNextControlName(myCue + "_Template");
				SECTR_AudioCue newTemplate = ObjectField<SECTR_AudioCue>("Template", "An optional reuse settings from another Cue.", oldTemplate, false);
				if(newTemplate != oldTemplate)
				{
					if(oldTemplate)
					{
						oldTemplate.RemoveTemplateRef();
					}
					templateProp.objectReferenceValue = newTemplate;
					if(newTemplate)
					{
						newTemplate.AddTemplateRef();
					}
				}
                if (newTemplate != null)
                {
                    if (GUILayout.Button("Remove Template"))
                    {
                        myCue.Template = null;
                    }
                }

			}
			else
			{
				GUI.enabled = false;
                //make sure this cue does not use a template itself, else it can get locked in circular references
                if (myCue.Template != null)
                {
                    myCue.Template.RemoveTemplateRef();
                    myCue.Template = null;
                }
                EditorGUILayout.IntField(new GUIContent("Template References", "Number of Cues that use this Cue as a template."), myCue.RefCount);
				GUI.enabled = wasEnabled;
			}
		}

		bool hasTemplate = myCue.Template != null;
		SECTR_AudioCue srcCue = myCue.SourceCue;
		GUI.enabled &= !hasTemplate;
		if(hasTemplate)
		{
			SetProxy(myCue.Template);
		}

		DrawCommon(myCue, srcCue);

		if(draw2D3D)
		{
			Draw2D3D(srcCue);
		}

		DrawParameters(myCue, srcCue);

		if(hasTemplate)
		{
			SetProxy(null);
		}
		GUI.enabled = wasEnabled;

		if(!multiSelect)
		{
			DrawAudioClips(myCue, srcCue);
		}

		if(srcCue.MinDistance > srcCue.MaxDistance)
		{
			GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
			warningStyle.alignment = TextAnchor.MiddleCenter;
			warningStyle.normal.textColor = Color.red;
			EditorGUILayout.LabelField("Max Distance is smaller than Min.", warningStyle);
			EditorGUILayout.LabelField("Cue will sound strange until fixed.", warningStyle);
		}
	}

	void DrawCommon(SECTR_AudioCue myCue, SECTR_AudioCue srcCue)
	{
		basicFoldout = EditorGUILayout.Foldout(basicFoldout, "Basic Properties");
		if(basicFoldout)
		{
			++EditorGUI.indentLevel;
			DrawProperty("Loops");
			DrawProperty("HDR");
			if(srcCue.HDR)
			{
				DrawMinMaxProperty("Loudness", 0f, 200f);
			}
			else
			{
				DrawMinMaxProperty("Volume", 0f, 1f);
			}
			DrawMinMaxProperty("Pitch", -3f, 3f);
			bool wasEnabled = GUI.enabled;
			float minLength = myCue.MinClipLength();
			GUI.enabled &= minLength > 0 || myCue.IsTemplate;
			DrawSliderProperty("FadeInTime", 0, minLength > 0 ? minLength : 1);
			DrawSliderProperty("FadeOutTime", 0, minLength > 0 ? minLength : 1);
            DrawProperty("KeepAliveTime");
            GUI.enabled = wasEnabled;
			DrawProperty("Spatialization");
			--EditorGUI.indentLevel;
		}

		advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced Properties");
		if(advancedFoldout)
		{
			++EditorGUI.indentLevel;
			DrawMinMaxProperty("Delay", 0, 100f);
			DrawProperty("PlayProbability");
			DrawProperty("Spread");
			DrawProperty("MaxInstances");
			DrawProperty("Priority");
			if(SECTR_Modules.HasPro())
			{
				DrawProperty("BypassEffects");
			}
			DrawProperty("Prefab");
			--EditorGUI.indentLevel;
		}
	}

	void Draw2D3D(SECTR_AudioCue srcCue)
	{
		// 2D/3D Stuff
		if(!srcCue.Is3D || !srcCue.IsLocal)
		{
			spatializationFoldout = EditorGUILayout.Foldout(spatializationFoldout, (srcCue.Is3D ? "3D" : "2D") + " Properties");
			if(spatializationFoldout)
			{
				++EditorGUI.indentLevel;
				if(srcCue.Is3D)
				{
					if(srcCue.Spatialization != SECTR_AudioCue.Spatializations.Infinite3D)
					{
						DrawProperty("Falloff");
						DrawProperty("MinDistance");
						DrawProperty("MaxDistance");
						DrawProperty("OcclusionScale");
						DrawProperty("DopplerLevel");
						DrawProperty("ProximityLimit");
						DrawProperty("ProximityRange");
					}
				}
				else
				{
					DrawProperty("Pan2D");
				}
				--EditorGUI.indentLevel;
			}
		}
	}

	void DrawParameters(SECTR_AudioCue myCue, SECTR_AudioCue srcCue)
	{
		if(myCue == srcCue || srcCue.ControlParams.Count > 0)
		{
			parameterFoldout = EditorGUILayout.Foldout(parameterFoldout, "Control Parameters");
			if(parameterFoldout)
			{
				++EditorGUI.indentLevel;
				int numCurves = srcCue.ControlParams.Count;
				int paramToRemove = -1;
				for(int curveIndex = 0; curveIndex < numCurves; ++curveIndex)
				{
					SECTR_CueParam param = srcCue.ControlParams[curveIndex];
					param.toggle = EditorGUILayout.Foldout(param.toggle, param.name);
					if(param.toggle)
					{
						bool wasEnabled = GUI.enabled;
						GUI.enabled = myCue == srcCue;
						++EditorGUI.indentLevel;
						EditorGUI.BeginChangeCheck();
						GUI.SetNextControlName(param.name + "_Name");
						param.name = EditorGUILayout.TextField("Name", param.name);
						param.affects = (SECTR_CueParam.TargetType)EditorGUILayout.EnumPopup("Affects", param.affects);
						if(param.affects == SECTR_CueParam.TargetType.Attribute)
						{
							++EditorGUI.indentLevel;
							if(srcCue.Prefab)
							{
								List<Component> prefabComponents = new List<Component>(srcCue.Prefab.GetComponents<Component>());
								Dictionary<Component, List<SECTR_CueParam.AttributeData>> componentProperties = new Dictionary<Component, List<SECTR_CueParam.AttributeData>>();
								int numPrefabComponents = prefabComponents.Count;
								int componentIndex = 0;
								while(componentIndex < numPrefabComponents)
								{
									if(prefabComponents[componentIndex].GetType() == typeof(AudioSource) ||
									   prefabComponents[componentIndex].GetType() == typeof(Transform))
									{
										prefabComponents.RemoveAt(componentIndex);
										--numPrefabComponents;
									}
									else
									{
										Component component = prefabComponents[componentIndex];
										List<SECTR_CueParam.AttributeData> attributes = null;
										
										FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
										foreach(FieldInfo field in fields)
										{
											object[] hide = field.GetCustomAttributes(typeof(HideInInspector), true);
											object[] obselete = field.GetCustomAttributes(typeof(ObsoleteAttribute), true);
											
											if(field.IsPublic &&
											   (hide == null || hide.Length == 0) &&
											   (obselete == null || obselete.Length == 0) &&
											   (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
											{
												SECTR_CueParam.AttributeData newField = new SECTR_CueParam.AttributeData();
												newField.ComponentType = component.GetType();
												newField.attributeName = field.Name;
												newField.fieldAttribute = true;
												if(!componentProperties.TryGetValue(component, out attributes))
												{
													attributes = new List<SECTR_CueParam.AttributeData>();
													componentProperties[component] = attributes;
												}
												attributes.Add(newField);
											}
										}
										
										PropertyInfo[] properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
										foreach(PropertyInfo property in properties)
										{
											object[] hide = property.GetCustomAttributes(typeof(HideInInspector), true);
											object[] obselete = property.GetCustomAttributes(typeof(ObsoleteAttribute), true);
											
											if(property.CanWrite &&
											   (hide == null || hide.Length == 0) &&
											   (obselete == null || obselete.Length == 0) &&
											   (property.PropertyType == typeof(float) || property.PropertyType == typeof(int)))
											{
												SECTR_CueParam.AttributeData newField = new SECTR_CueParam.AttributeData();
												newField.ComponentType = component.GetType();
												newField.attributeName = property.Name;
												newField.fieldAttribute = false;
												
												if(!componentProperties.TryGetValue(component, out attributes))
												{
													attributes = new List<SECTR_CueParam.AttributeData>();
													componentProperties[component] = attributes;
												}
												attributes.Add(newField);
											}
										}
										
										if(attributes != null && attributes.Count > 0)
										{
											++componentIndex;
										}
										else
										{
											prefabComponents.RemoveAt(componentIndex);
											--numPrefabComponents;
										}
									}
								}
								
								if(prefabComponents.Count > 0)
								{
									int selectedComponent = 0;
									string[] componentNames = new string[prefabComponents.Count];
									for(componentIndex = 0; componentIndex < prefabComponents.Count; ++componentIndex)
									{
										componentNames[componentIndex] = prefabComponents[componentIndex].GetType().Name;
										if(param.attributeData.ComponentType != null &&
										   componentNames[componentIndex] == param.attributeData.ComponentType.Name)
										{
											selectedComponent = componentIndex;
										}
									}
									selectedComponent = EditorGUILayout.Popup("Component", selectedComponent, componentNames);
									param.attributeData.ComponentType = prefabComponents[selectedComponent].GetType();
									
									List<SECTR_CueParam.AttributeData> properties = componentProperties[prefabComponents[selectedComponent]];
									string[] propertyNames = new string[properties.Count];
									int selectedProperty = 0;
									for(int propertyIndex = 0; propertyIndex < properties.Count; ++propertyIndex)
									{
										propertyNames[propertyIndex] = properties[propertyIndex].attributeName;
										if(propertyNames[propertyIndex] == param.attributeData.attributeName)
										{
											selectedProperty = propertyIndex;
										}
									}
									
									selectedProperty = EditorGUILayout.Popup("Attribute", selectedProperty, propertyNames);
									param.attributeData.attributeName = propertyNames[selectedProperty];
								}
								else
								{
									EditorGUILayout.LabelField("Prefab does not contain any RPC compatible components.");
								}
							}
							else
							{
								EditorGUILayout.LabelField("Component target requires Prefab.");
							}
							--EditorGUI.indentLevel;
						}

						float curveMin = 0;
						float curveMax = 0;
						if(param.curve.keys.Length > 0)
						{
							curveMin = param.curve.keys[0].time;
							curveMax = param.curve.keys[param.curve.keys.Length - 1].time;
						}

						GUI.SetNextControlName(param.attributeData.attributeName + "_Param_Default");
						param.defaultValue = EditorGUILayout.Slider("Default", param.defaultValue, curveMin, curveMax);
						param.defaultValue = Mathf.Clamp(param.defaultValue, curveMin, curveMax);
						GUI.SetNextControlName(param.attributeData.attributeName + "_Param_Curve");
						param.curve = EditorGUILayout.CurveField("Curve", param.curve);

						if(EditorGUI.EndChangeCheck())
						{
							EditorUtility.SetDirty(myCue);
						}

						if(myCue == srcCue)
						{
							Rect controlRect = EditorGUILayout.GetControlRect(true);
							Rect valueRect = EditorGUI.PrefixLabel(controlRect, 0, new GUIContent(""));
							if(GUI.Button(valueRect, "Remove " + param.name + " Parameter"))
							{
								if(EditorUtility.DisplayDialog("Confirm Parameter Deletion", "Are you sure you wish to delete " + param.name + "parameter?", "Yes", "No"))
								{
									paramToRemove = curveIndex;
								}
							}
						}

						--EditorGUI.indentLevel;
						GUI.enabled = wasEnabled;
					}
					srcCue.ControlParams[curveIndex] = param;
				}
				
				if(myCue == srcCue)
				{
					Rect controlRect = EditorGUILayout.GetControlRect(true);
					Rect valueRect = EditorGUI.PrefixLabel(controlRect, 0, new GUIContent(""));
					if(GUI.Button(valueRect, "Add Parameter"))
					{
						SECTR_Undo.Record(myCue, "Added parameter");
						myCue.ControlParams.Add(new SECTR_CueParam());
						EditorUtility.SetDirty(myCue);
					}
				}

				if(paramToRemove >= 0)
				{
					SECTR_Undo.Record(myCue, "Removed parameter");
					myCue.ControlParams.RemoveAt(paramToRemove);
					EditorUtility.SetDirty(myCue);
				}

				--EditorGUI.indentLevel;
			}
		}
	}

	void DrawAudioClips(SECTR_AudioCue myCue, SECTR_AudioCue srcCue)
	{
		clipFoldout = EditorGUILayout.Foldout(clipFoldout, "Audio Clips");
		if(clipFoldout)
		{
			++EditorGUI.indentLevel;
		
			bool hasClips = myCue.AudioClips.Count > 0;

			int lineHeight = (int)EditorGUIUtility.singleLineHeight;
			int headerHeight = 25;
			int iconSize = lineHeight;

			// Column labels
			Rect headerRect = EditorGUILayout.BeginHorizontal();
			GUI.Box(headerRect, GUIContent.none);
			EditorGUILayout.LabelField(GUIContent.none, GUILayout.Width(iconSize * 2), GUILayout.MaxWidth(iconSize * 2), GUILayout.MinWidth(iconSize * 2), GUILayout.ExpandWidth(false), GUILayout.Height(headerHeight));

			string[] categories = {
				"CLIP",
				"VOLUME",
				"REMOVE",
			};
			float[] widthScales = {
				1.6f,
				0.7f,
				0.7f,
			};
			int[] widths = new int[categories.Length];
			int baseColumnWidth = (int)(headerRect.width / categories.Length);

			clipStyle.fontStyle = FontStyle.Bold;
			clipStyle.alignment = TextAnchor.MiddleCenter;
			
			int columnSum = 0;
			for(int catIndex = 0; catIndex < categories.Length; ++catIndex)
			{
				int width = (int)(widthScales[catIndex] * baseColumnWidth);
				GUI.Label(new Rect(columnSum + headerRect.x, headerRect.y, width, headerRect.height), categories[catIndex], clipStyle);
				columnSum += width;
				widths[catIndex] = width;
			}

			clipStyle.fontStyle = FontStyle.Normal;
			
			EditorGUILayout.EndHorizontal();

			Rect clipAreaRect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(100));
			EditorGUILayout.Space();

			bool wasEnabled = GUI.enabled;
			GUI.enabled = false;
			GUI.Button(clipAreaRect, GUIContent.none);
			GUI.enabled = wasEnabled;

			int currentClipIndex = -1;
			int numClips = myCue.AudioClips.Count;
			bool clipProblem = false;
			bool panProblem = false;
			bool hdrProblem = false;
			int clipToRemove = -1;
			for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
			{
				SECTR_AudioCue.ClipData clipData = myCue.AudioClips[clipIndex];
				if(clipData != null && clipData.Clip != null)
				{
					AudioClip clip = clipData.Clip;

					bool reallyWasEnabled = GUI.enabled;
					GUI.enabled = true;
					Rect clipRect = EditorGUILayout.BeginHorizontal();
					if(GUILayout.Button(new GUIContent(playIcon, "Plays this AudioClip."), iconButtonStyle, GUILayout.Width(lineHeight), GUILayout.Height(lineHeight)))
					{
						SECTR_AudioSystem.Audition(clip);
					}
					GUI.enabled = reallyWasEnabled;

					int checkSize = 20;
					int columnIndex = 0;
					int columnWidth = 0;
					float rowY = clipRect.y + 1;
					columnSum = (int)clipRect.x;

					if(srcCue.Pan2D != 0 && clip.channels > 2)
					{
						panProblem = true;
					}

					if(srcCue.HDR && !clipData.HDRKeysValid())
					{
						hdrProblem = true;
					}

                    clipStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.gray : Color.black;
                    clipStyle.alignment = TextAnchor.MiddleLeft;

					// Name
					columnWidth = widths[columnIndex++];
					float shift = iconSize * 1.5f;
					GUI.Label(new Rect(columnSum + shift, rowY, columnWidth - shift, clipRect.height), clip.name, clipStyle);
					columnSum += columnWidth;

					// Volume
					columnWidth = widths[columnIndex++];
					int labelWidth = 40;
					float oldVolume = clipData.Volume;
					GUI.SetNextControlName(clip.name + "_Volume");
					float newVolume = EditorGUI.FloatField(new Rect(columnSum - labelWidth * 0.6f + columnWidth * 0.5f, rowY, labelWidth, clipRect.height), oldVolume);
					if(newVolume != oldVolume)
					{
						SECTR_Undo.Record(myCue, "Changed Clip Volume");
						clipData.Volume = newVolume;
						EditorUtility.SetDirty(myCue);
					}
					columnSum += columnWidth;

					// Remove Button
					columnWidth = widths[columnIndex++];
					if(GUI.Button(new Rect(columnSum - checkSize * 0.5f + columnWidth * 0.5f, rowY, checkSize, clipRect.height), new GUIContent(removeIcon, "Removes AudioClip from Cue")))
					{
						clipToRemove = clipIndex;
					}
					columnSum += columnWidth;

					EditorGUILayout.EndHorizontal();

					if(Event.current.type == EventType.ContextClick && clipRect.Contains(Event.current.mousePosition))
					{
						currentClipIndex = clipIndex;
					}

					if(srcCue.HDR)
					{
						if(clipData.HDRCurve != null && clipData.HDRCurve.length > 0)
						{
							EditorGUI.BeginChangeCheck();
							GUI.SetNextControlName(clip.name + "_HDREnvelope");
							EditorGUILayout.CurveField("HDR Envelope", clipData.HDRCurve);
							if(EditorGUI.EndChangeCheck())
							{
								SECTR_Undo.Record(myCue, "Changed HDR Curve");
							}
						}
						else
						{
							bool reallyReallyWasEnabled = GUI.enabled;
							GUI.enabled = false;
							GUI.SetNextControlName(clip.name + "_HDREnvelope");
							EditorGUILayout.CurveField("HDR Envelope", dummyCurve);
							GUI.enabled = reallyReallyWasEnabled;
						}
					}
				}
				else
				{
					if(GUILayout.Button(new GUIContent("Remove NULL Clip", "Removes NULL audio clip reference.")))
					{
						clipToRemove = clipIndex;
					}
				}
			}

			if(clipToRemove >= 0)
			{
				SECTR_Undo.Record(myCue, "Removed Clip");
				myCue.RemoveClip(clipToRemove);
			}

			EditorGUILayout.Space();
			if(GUI.enabled)
			{
				bool reallyWasEnabled = GUI.enabled;
				GUI.enabled = false;
				clipStyle.alignment = TextAnchor.MiddleCenter;
				EditorGUILayout.LabelField("Drag in Additional Audio Clips", clipStyle);
				GUI.enabled = reallyWasEnabled;
			}
			EditorGUILayout.EndVertical();
			
			if(myCue.AudioClips.Count > 1)
			{
				DrawProperty("PlaybackMode");
			}

			wasEnabled = GUI.enabled;
			GUI.enabled = myCue.AudioClips.Count > 0;
			if(SECTR_AudioSystem.IsAuditioning())
			{
				if(GUILayout.Button(new GUIContent("Stop Audition", "Stops auditioning of this AudioCue.")))
				{
					SECTR_AudioSystem.StopAudition();
				}
			}
			else
			{
				if(GUILayout.Button(new GUIContent("Audition", "Selects and play and AudioClip from this AudioCue.")))
				{
					SECTR_AudioSystem.Audition(myCue);
				}
			}
			GUI.enabled = wasEnabled;

			if(clipProblem)
			{
				clipStyle.alignment = TextAnchor.MiddleCenter;
				clipStyle.normal.textColor = Color.red;
				GUILayout.Label("Warning: Cue and Clips have Mismatched 3D settings. Please Fix.", clipStyle);
			}
								
			if(panProblem)
			{
				clipStyle.alignment = TextAnchor.MiddleCenter;
				clipStyle.normal.textColor = Color.red;
				GUILayout.Label("Warning: Pan2D has no effect on clips with more than two channels.", clipStyle);
			}

			if(hdrProblem)
			{
				if(bakeMaster)
				{
					Rect controlRect = EditorGUILayout.GetControlRect();
					EditorGUI.ProgressBar(controlRect, bakeMaster.Progress, "Baking HDR Data");
				}
				else
				{
					clipStyle.alignment = TextAnchor.MiddleCenter;
					clipStyle.normal.textColor = Color.red;
					GUILayout.Label("Warning: Cue is missing some HDR data. Bake HDR Keys for higher quality sound.", clipStyle);
					if(GUILayout.Button("Bake HDR Keys"))
					{
						List<SECTR_AudioCue> bakeList = new List<SECTR_AudioCue>(1);
						bakeList.Add(myCue);
						bakeMaster = SECTR_ComputeRMS.BakeList(bakeList);
					}
				}
			}

			// Event handling
			if(Event.current.type == EventType.ContextClick && clipAreaRect.Contains(Event.current.mousePosition))
			{
				GenericMenu menu = new GenericMenu();

				if(hasClips)
				{
					if(currentClipIndex >= 0)
					{
						menu.AddItem(new GUIContent("Remove " + myCue.AudioClips[currentClipIndex].Clip.name), false, delegate() 
						{
							SECTR_Undo.Record(myCue, "Removed Clip");
							myCue.RemoveClip(currentClipIndex);
						});
					}

					menu.AddItem(new GUIContent("Clear All Clips"), false, delegate() 
					{
						myCue.ClearClips();
					});
				}
				menu.ShowAsContext();
			}
			
			if(clipAreaRect.Contains(Event.current.mousePosition))
			{
				int numDropObjects = DragAndDrop.objectReferences.Length;
				if(numDropObjects > 0)
				{
					if(Event.current.type == EventType.DragUpdated)
					{
						DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
					}
					else if(Event.current.type == EventType.DragPerform)
					{
						for(int dropIndex = 0; dropIndex < numDropObjects; ++dropIndex)
						{
							AudioClip clip = DragAndDrop.objectReferences[dropIndex] as AudioClip;
							if(clip != null)
							{
								SECTR_Undo.Record(myCue, "Add Clip");
								myCue.AddClip(clip, false);
								DragAndDrop.AcceptDrag();
							}
						}
					}
				}
			}

			--EditorGUI.indentLevel;
		}
	}
	#endregion
}
