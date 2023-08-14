// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_TriggerSource))]
[CanEditMultipleObjects]
public class SECTR_TriggerSourceEditor : SECTR_PointSourceEditor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		DrawProperty("Cue");
		DrawProperty("Loop");
		DrawPitchVolume();
		DrawPlayButton();
		serializedObject.ApplyModifiedProperties();
	}
}
