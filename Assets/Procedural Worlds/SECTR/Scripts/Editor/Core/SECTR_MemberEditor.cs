// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SECTR_Member))]
[CanEditMultipleObjects]
public class SECTR_MemberEditor : SECTR_Editor
{
	public override void OnInspectorGUI()
	{
		SECTR_Member myMember = (SECTR_Member)target;
		serializedObject.Update();
		if(!myMember.IsSector)
		{
			DrawProperty("PortalDetermined");
			DrawProperty("ForceStartSector");
		}
        if (!myMember.gameObject.isStatic)
        {
            DrawProperty("BoundsUpdateMode");
        }

        //Drawing manual inputs for static fields
        GUIContent memberUpdateContent = new GUIContent("Member Update Mode", "Global Setting. Determines how often the Sector Members are re-evaluated. More frequent updates requires more CPU. Switch to delayed or Save & Export only if experiencing Editor Slowdowns with many Sectors in the scene.");
        SECTR_Member.MemberUpdateMode = (SECTR_MemberUpdateMode)EditorGUILayout.EnumPopup(memberUpdateContent, SECTR_Member.MemberUpdateMode);

        if (SECTR_Member.MemberUpdateMode == SECTR_MemberUpdateMode.Delayed)
        {
            GUIContent updateDelayContent = new GUIContent("Update Delay", "Global Setting. Delay in Milliseconds until Sector Members are re-evaluated in delayed mode.");
            SECTR_Member.MemberUpdateDelay = EditorGUILayout.IntField(updateDelayContent, SECTR_Member.MemberUpdateDelay);
        }

        //Update the editorPrefs
        EditorPrefs.SetInt(SECTR.PWSECTRPrefKeys.m_memberUpdateMode, (int)SECTR_Member.MemberUpdateMode);
        EditorPrefs.SetInt(SECTR.PWSECTRPrefKeys.m_memberUpdateDelay, SECTR_Member.MemberUpdateDelay); 


        if (SECTR_Member.MemberUpdateMode != SECTR_MemberUpdateMode.Realtime)
        {
            if (GUILayout.Button("Force Member Update"))
            {
                //execute the update for the target in any case, e.g. if Inspector window is locked
                myMember.UpdateMembers();

                //then check if there are any sectors in the current selection so that the button can be used for multi select
                foreach (GameObject go in Selection.gameObjects)
                {
                    SECTR_Member sm = go.GetComponent<SECTR_Member>();
                    if (sm != null)
                    {
                        sm.UpdateMembers();
                    }
                }
            }
        }

		DrawProperty("ExtraBounds");
		DrawProperty("OverrideBounds");
		DrawProperty("BoundsOverride");
		if(SECTR_Modules.VIS)
		{
			DrawProperty("ChildCulling");
			DrawProperty("DirShadowCaster");
			DrawProperty("DirShadowDistance");
            DrawProperty("ignoreTransforms");
        }
		serializedObject.ApplyModifiedProperties();
	}
}
