// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_AudioSource))]
[CanEditMultipleObjects]
public class SECTR_AudioSourceEditor : SECTR_Editor
{
	protected void OnSceneGUI()
	{
		SECTR_AudioSource mySource = (SECTR_AudioSource)target;
		if(mySource.Cue && mySource.Cue.SourceCue && !mySource.Cue.SourceCue.IsLocal)
		{
			bool editable = SECTR_VC.IsEditable(AssetDatabase.GetAssetPath(mySource.Cue.SourceCue));
			Handles.color = editable ? new Color(127f / 255f, 178f / 255f, 253f / 255f) : Color.gray;
			mySource.Cue.SourceCue.MinDistance = Handles.RadiusHandle(Quaternion.identity, mySource.transform.position, mySource.Cue.SourceCue.MinDistance);
			mySource.Cue.SourceCue.MaxDistance = Handles.RadiusHandle(Quaternion.identity, mySource.transform.position, mySource.Cue.SourceCue.MaxDistance);

			if(GUI.changed)
			{
				EditorUtility.SetDirty(mySource.Cue.SourceCue);
			}
		}
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		DrawPitchVolume();
		DrawPlayButton();
	}

	protected void DrawPitchVolume()
	{
		SECTR_AudioSource mySource = (SECTR_AudioSource)target;

		float oldVolume = mySource.Volume;
		float newVolume = EditorGUILayout.Slider(new GUIContent("Volume", "Audio Source volume scale."), oldVolume, 0f, 1f);
		
		float oldPitch = mySource.Pitch;
		float newPitch = EditorGUILayout.Slider(new GUIContent("Pitch", "Audio Source pitch scale."), oldPitch, 0f, 2f);
		
		if(newVolume != oldVolume || newPitch != oldPitch)
		{
			SECTR_Undo.Record(mySource, "Changed Volume/Pitch");
			mySource.Volume = newVolume;
			mySource.Pitch = newPitch;
		}
	}

	protected void DrawPlayButton()
	{
		SECTR_AudioSource mySource = (SECTR_AudioSource)target;
		bool wasEnabled = GUI.enabled;
		GUI.enabled &= mySource.Cue != null && mySource.enabled;
		if(mySource.IsPlaying && GUILayout.Button(new GUIContent("Stop", "Stops currently playing instance.")))
		{
			mySource.Stop(false);
		}
		else if(!mySource.IsPlaying && GUILayout.Button(new GUIContent("Play", "Starts playing the Cue in the world.")))
		{
			mySource.Play();
		}
		GUI.enabled = wasEnabled;
	}
}
