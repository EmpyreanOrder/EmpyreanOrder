// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_AudioBus))]
public class SECTR_AudioBusEditor : SECTR_Editor
{
	Texture muteOnIcon = null;
	Texture muteOffIcon = null;
	Texture soloOnIcon = null;
	Texture soloOffIcon = null;

	public override void OnEnable()
	{
		base.OnEnable();
		muteOnIcon = SECTR_AudioWindow.LoadIcon("MuteOnIcon.psd");
		muteOffIcon = SECTR_AudioWindow.LoadIcon("MuteOffIcon.psd");
		soloOnIcon = SECTR_AudioWindow.LoadIcon("SoloOnIcon.psd");
		soloOffIcon = SECTR_AudioWindow.LoadIcon("SoloOffIcon.psd");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		SECTR_AudioBus myBus = (SECTR_AudioBus)target;
		SECTR_AudioBus oldParent = myBus.Parent;
		SECTR_AudioBus newParent = ObjectField<SECTR_AudioBus>("Parent Bus", "The hierarchical parent of this Bus.", oldParent, false);
		if(newParent != oldParent)
		{
			SECTR_Undo.Record(myBus, "Edit Bus Parent");
			myBus.Parent = newParent;
		}
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		DrawBusControls("", 100, myBus, muteOnIcon, muteOffIcon, soloOnIcon, soloOffIcon, GUI.skin.label, GUI.skin.verticalSlider, EditorStyles.textField);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
	}

	public static void DrawBusControls(string name, float width, SECTR_AudioBus Bus, Texture muteOnIcon, Texture muteOffIcon, Texture soloOnIcon, Texture soloOffIcon, GUIStyle elementStyle, GUIStyle busSliderStyle, GUIStyle busFieldStyle)
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(width)); // Bus Block
		if(!string.IsNullOrEmpty(name))
		{
			elementStyle.normal.textColor = SECTR_Window.UnselectedItemColor;
			elementStyle.alignment = TextAnchor.MiddleCenter;
			elementStyle.fontStyle = FontStyle.Bold;
			GUILayout.Label(name, elementStyle);
			elementStyle.fontStyle = FontStyle.Normal;
		}
		else
		{
			EditorGUILayout.Space();
		}
			
		EditorGUILayout.BeginHorizontal(); // Solo and Mute buttons
		bool busMuted = Bus.Muted;
		GUI.SetNextControlName("AudioBus_Mute");
		if(GUILayout.Button(new GUIContent(busMuted ? muteOnIcon : muteOffIcon, "Changes mute state of this bus."), elementStyle))
		{
			SECTR_Undo.Record(Bus, "Muted Bus");
			Bus.Muted = !busMuted;
			EditorUtility.SetDirty(Bus);
		}
		bool busSoloed = SECTR_AudioSystem.BusIsSolo(Bus);
		GUI.SetNextControlName("AudioBus_Solo");
		if(GUILayout.Button(new GUIContent(busSoloed ? soloOnIcon : soloOffIcon, "Only sounds played through this bus will be audible."), elementStyle))
		{
			SECTR_Undo.Record(Bus, "Solod Bus");
			SECTR_AudioSystem.Solo(Bus, !busSoloed);
			EditorUtility.SetDirty(Bus);
		}
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.BeginHorizontal(); // Sliders
		
		// Volume Slider
		EditorGUILayout.BeginVertical(); // Start Volume
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		float oldVolume = Bus.Volume;
		GUI.SetNextControlName("AudioBus_VolumeSlider");
        busSliderStyle.stretchHeight = true;
        busSliderStyle.fixedHeight = 200;
        busSliderStyle.margin = new RectOffset(0,10,0,0);
		float newVolume = GUILayout.VerticalSlider(oldVolume, 1, 0, busSliderStyle, GUI.skin.verticalSliderThumb);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		
		// Volume Field
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUI.SetNextControlName("AudioBus_VolumeField");
		newVolume = EditorGUILayout.FloatField(newVolume, busFieldStyle, GUILayout.Width(50));
		
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		
		// Volume Label
		GUILayout.Label("Volume", elementStyle);
		EditorGUILayout.EndHorizontal(); // End Volume
		
		// Pitch Slider
		EditorGUILayout.BeginVertical(); // Start Volume
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		float oldPitch = Bus.Pitch;
		GUI.SetNextControlName("AudioBus_PitchSlider");
		float newPitch = GUILayout.VerticalSlider(oldPitch, 2, 0, busSliderStyle, GUI.skin.verticalSliderThumb);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		
		// Pitch Field
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUI.SetNextControlName("AudioBus_PitchField");
		newPitch = EditorGUILayout.FloatField(newPitch, busFieldStyle, GUILayout.Width(50));
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		
		// Pitch Label
		GUILayout.Label("Pitch", elementStyle);
		EditorGUILayout.EndHorizontal(); // End Pitch
		
		EditorGUILayout.EndVertical(); // End Sliders
		
		EditorGUILayout.EndVertical(); // End Bus
		
		if(oldVolume != newVolume || oldPitch != newPitch)
		{
			SECTR_Undo.Record(Bus, "Changed Bus");
			Bus.Volume = newVolume;
			Bus.Pitch = newPitch;
			EditorUtility.SetDirty(Bus);
		}
	}
}
