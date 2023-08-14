// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_AudioSystem))]
[CanEditMultipleObjects]
public class SECTR_AudioSystemEditor : SECTR_Editor
{
	bool foldoutAdvanced = true;
	bool foldoutHDR = true;
	bool foldoutNearby = true;
	bool foldoutOcclusion = true;

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		SECTR_AudioSystem mySystem = (SECTR_AudioSystem)target;

		DrawProperty("MaxInstances");
		if(SECTR_Modules.HasPro())
		{
			DrawSliderProperty("LowpassInstances", 0, mySystem.MaxInstances);
		}
		DrawProperty("MasterBus");
		DrawProperty("DefaultAmbience");
		DrawProperty("ShowAudioHUD");

		foldoutHDR = EditorGUILayout.Foldout(foldoutHDR, "HDR");
		if(foldoutHDR)
		{
			DrawProperty("HDRBaseLoudness");
			DrawProperty("HDRWindowSize");
			DrawProperty("HDRDecay");
		}

		foldoutNearby = EditorGUILayout.Foldout(foldoutNearby, "Near 2D Blend");
		if(foldoutNearby)
		{
			DrawProperty("BlendNearbySounds");
			DrawMinMaxProperty("NearBlendRange", 0, 2);
		}

		foldoutOcclusion = EditorGUILayout.Foldout(foldoutOcclusion, "Occlusion");
		if(foldoutOcclusion)
		{
			DrawProperty("OcclusionFlags");
			DrawProperty("RaycastLayers");
			DrawProperty("OcclusionDistance");
			DrawProperty("OcclusionVolume");
			if(SECTR_Modules.HasPro())
			{
				DrawProperty("OcclusionCutoff");
				DrawProperty("OcclusionResonanceQ");
			}
		}

		foldoutAdvanced = EditorGUILayout.Foldout(foldoutAdvanced, "Advanced");
		if(foldoutAdvanced)
		{
			DrawMinMaxProperty("RetestInterval", 0, 10);
			DrawProperty("CullingBuffer");
			DrawProperty("Debugging");
		}
		serializedObject.ApplyModifiedProperties();
	}
}
