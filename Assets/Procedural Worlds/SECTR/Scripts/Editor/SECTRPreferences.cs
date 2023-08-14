using UnityEditor;
using UnityEngine;

namespace SECTR
{
    public class PWSECTRPrefKeys
    {
        internal const string m_memberUpdateMode = "PW.SECTR_MemberUpdateMode";
        internal const string m_memberUpdateDelay = "PW.SECTR_MemberUpdateDelay";
        internal const string m_autoSaveOnExport = "PW.SECTR_AutoSaveOnExport";
        internal const string m_recycleScenesOnExport = "PW.SECTR_RecycleScenesOnExport";
        internal const string m_autoRefresh = "PW.SECTR_AutoRefresh";
    }

    public static class SECTRPreferences
    {
        public static bool m_refreshSettings = false;

        private static GUIStyle m_boxStyle;
        private static SECTR_MemberUpdateMode memberUpdateMode;
        private static int memberUpdateDelay;
        private static bool autoSaveOnExport;
        private static bool recycleScenesOnExport;
        private static bool autoRefresh;

#if !UNITY_2019_1_OR_NEWER
        [PreferenceItem("SECTR")]
        public static void PreferenceGUI()
        {
            //Load settings
            if (!m_refreshSettings)
            {
                Load();
            }

            //Set up the box style
            if (m_boxStyle == null)
            {
                m_boxStyle = new GUIStyle(GUI.skin.box);
                m_boxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                m_boxStyle.fontStyle = FontStyle.Bold;
                m_boxStyle.alignment = TextAnchor.UpperLeft;
            }

            //header info
            EditorGUILayout.LabelField("SECTR Preferences allows you to modify some global settings to suit your development.", EditorStyles.largeLabel);

            EditorGUILayout.BeginVertical(m_boxStyle);
            EditorGUILayout.LabelField("SECTR Member Settings", EditorStyles.boldLabel);
            autoRefresh = EditorGUILayout.Toggle(new GUIContent("Auto Refresh", "Automatically refreshes the Sectr components in the scene with the updated preferences. Leave this disabled if you don't want it to override any settings you already have setup on the Sectr Member components."), autoRefresh);
            memberUpdateMode = (SECTR_MemberUpdateMode)EditorGUILayout.EnumPopup(new GUIContent("Member Update Mode", "Global Setting. Determines how often the Sector Members are re-evaluated. More frequent updates requires more CPU. Switch to delayed or Save & Export only if experiencing Editor Slowdowns with many Sectors in the scene."), memberUpdateMode);
            if (memberUpdateMode == SECTR_MemberUpdateMode.Delayed)
            {
                memberUpdateDelay = EditorGUILayout.IntField(new GUIContent("Update Delay", "Global Setting. Delay in Milliseconds until Sector Members are re-evaluated in delayed mode."), memberUpdateDelay);
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Stream Settings", EditorStyles.boldLabel);
            autoSaveOnExport = EditorGUILayout.Toggle(new GUIContent("Auto Save on Export", "Automatically save the scene whenever a sector is exported"), autoSaveOnExport);
            recycleScenesOnExport = EditorGUILayout.Toggle(new GUIContent("Recycle Exported Scenes", "Re-use the existing scene chunks during export, rather than deleting & creating them from scratch. Recycling takes a bit more time on export by re-using the scenes, but does in return not re-create entries for new scenes in the build settings."), recycleScenesOnExport);

            EditorGUILayout.EndVertical();
            //Copyrights
            DrawFooter("SECTR...", 2020, true);

            if (CheckPrefrences())
            {
                //Set all the settings and editor prefs
                SetPrefs();
            }
        }
#else
        [SettingsProvider]
        public static SettingsProvider PreferenceGUIGlobal()
        {
            return new SettingsProvider("Preferences/Procedural Worlds/SECTR", SettingsScope.User)
            {
                guiHandler = searchContext =>
                {
                    //Load settings
                    if (!m_refreshSettings)
                    {
                        Load();
                    }

                    //Set up the box style
                    if (m_boxStyle == null)
                    {
                        m_boxStyle = new GUIStyle(GUI.skin.box);
                        m_boxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                        m_boxStyle.fontStyle = FontStyle.Bold;
                        m_boxStyle.alignment = TextAnchor.UpperLeft;
                    }

                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUILayout.LabelField("SECTR Member Settings", EditorStyles.boldLabel);
                    autoRefresh = EditorGUILayout.Toggle(new GUIContent("Auto Refresh", "Automatically refreshes the Sectr components in the scene with the updated preferences. Leave this disabled if you don't want it to override any settings you already have setup on the Sectr Member components."), autoRefresh);
                    memberUpdateMode = (SECTR_MemberUpdateMode)EditorGUILayout.EnumPopup(new GUIContent("Member Update Mode", "Global Setting. Determines how often the Sector Members are re-evaluated. More frequent updates requires more CPU. Switch to delayed or Save & Export only if experiencing Editor Slowdowns with many Sectors in the scene."), memberUpdateMode);
                    if (memberUpdateMode == SECTR_MemberUpdateMode.Delayed)
                    {
                        memberUpdateDelay = EditorGUILayout.IntField(new GUIContent("Update Delay", "Global Setting. Delay in Milliseconds until Sector Members are re-evaluated in delayed mode."), memberUpdateDelay);
                    }
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Stream Settings", EditorStyles.boldLabel);
                    autoSaveOnExport = EditorGUILayout.Toggle(new GUIContent("Auto Save on Export", "Automatically save the scene whenever a sector is exported"), autoSaveOnExport);
                    recycleScenesOnExport = EditorGUILayout.Toggle(new GUIContent("Recycle Exported Scenes", "Re-use the existing scene chunks during export, rather than deleting & creating them from scratch. Recycling takes a bit more time on export by re-using the scenes, but does in return not re-create entries for new scenes in the build settings."), recycleScenesOnExport);

                    EditorGUILayout.EndVertical();
                    //Copyrights
                    DrawFooter("SECTR...", 2020, true);

                    if (CheckPrefrences())
                    {
                        //Set all the settings and editor prefs
                        SetPrefs();
                    }
                }
            };
        }
#endif

        /// <summary>
        /// Draws the footer displayed at the bottom of the window
        /// </summary>
        /// <param name="message"></param>
        private static void DrawFooter(string productName, int currentYear, bool pinToBottom)
        {
            if (pinToBottom)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField(productName + " Copyright © " + currentYear.ToString() + " Procedural Worlds Pty Limited. All Rights Reserved.", EditorStyles.toolbar);
        }

        /// <summary>
        /// Load all settings
        /// </summary>
        private static void Load()
        {
            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_memberUpdateMode))
            {
                memberUpdateMode = (SECTR_MemberUpdateMode)EditorPrefs.GetInt(PWSECTRPrefKeys.m_memberUpdateMode);
            }
            else
            {
                memberUpdateMode = 0;
                EditorPrefs.SetInt(PWSECTRPrefKeys.m_memberUpdateMode, (int)memberUpdateMode);
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_memberUpdateDelay))
            {
                memberUpdateDelay = EditorPrefs.GetInt(PWSECTRPrefKeys.m_memberUpdateDelay);
            }
            else
            {
                memberUpdateDelay = 500;
                EditorPrefs.SetInt(PWSECTRPrefKeys.m_memberUpdateDelay, memberUpdateDelay);
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_autoSaveOnExport))
            {
                autoSaveOnExport = EditorPrefs.GetBool(PWSECTRPrefKeys.m_autoSaveOnExport);
            }
            else
            {
                autoSaveOnExport = false;
                EditorPrefs.SetBool(PWSECTRPrefKeys.m_autoSaveOnExport, autoSaveOnExport);
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_recycleScenesOnExport))
            {
                recycleScenesOnExport = EditorPrefs.GetBool(PWSECTRPrefKeys.m_recycleScenesOnExport);
            }
            else
            {
                recycleScenesOnExport = false;
                EditorPrefs.SetBool(PWSECTRPrefKeys.m_recycleScenesOnExport, recycleScenesOnExport);
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_autoRefresh))
            {
                autoRefresh = EditorPrefs.GetBool(PWSECTRPrefKeys.m_autoRefresh);
            }
            else
            {
                autoRefresh = false;
                EditorPrefs.SetBool(PWSECTRPrefKeys.m_autoRefresh, autoRefresh);
            }
        }

        /// <summary>
        /// Check if prefs need to be set
        /// </summary>
        /// <param name="skyProfiles"></param>
        /// <returns></returns>
        private static bool CheckPrefrences()
        {
            bool updateChanges = false;
            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_memberUpdateMode))
            {
                if (EditorPrefs.GetInt(PWSECTRPrefKeys.m_memberUpdateMode) != (int)memberUpdateMode)
                {
                    updateChanges = true;
                }
            }
            else
            {
                updateChanges = true;
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_memberUpdateDelay))
            {
                if (EditorPrefs.GetInt(PWSECTRPrefKeys.m_memberUpdateDelay) != memberUpdateDelay)
                {
                    updateChanges = true;
                }
            }
            else
            {
                updateChanges = true;
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_autoSaveOnExport))
            {
                if (EditorPrefs.GetBool(PWSECTRPrefKeys.m_autoSaveOnExport) != autoSaveOnExport)
                {
                    updateChanges = true;
                }
            }
            else
            {
                updateChanges = true;
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_recycleScenesOnExport))
            {
                if (EditorPrefs.GetBool(PWSECTRPrefKeys.m_recycleScenesOnExport) != recycleScenesOnExport)
                {
                    updateChanges = true;
                }
            }
            else
            {
                updateChanges = true;
            }

            if (EditorPrefs.HasKey(PWSECTRPrefKeys.m_autoRefresh))
            {
                if (EditorPrefs.GetBool(PWSECTRPrefKeys.m_autoRefresh) != autoRefresh)
                {
                    updateChanges = true;
                }
            }
            else
            {
                updateChanges = true;
            }

            return updateChanges;
        }

        /// <summary>
        /// Sets all the settings and prefs
        /// </summary>
        /// <param name="skyProfiles"></param>
        private static void SetPrefs()
        {
            EditorPrefs.SetInt(PWSECTRPrefKeys.m_memberUpdateMode, (int)memberUpdateMode);
            EditorPrefs.SetInt(PWSECTRPrefKeys.m_memberUpdateDelay, memberUpdateDelay);
            EditorPrefs.SetBool(PWSECTRPrefKeys.m_autoSaveOnExport, autoSaveOnExport);
            EditorPrefs.SetBool(PWSECTRPrefKeys.m_recycleScenesOnExport, recycleScenesOnExport);
            EditorPrefs.SetBool(PWSECTRPrefKeys.m_autoRefresh, autoRefresh);
            if (autoRefresh)
            {
                SetSectrMemberSettings(memberUpdateMode, memberUpdateDelay);
            }
        }

        /// <summary>
        /// Refreshes the settings
        /// </summary>
        /// <param name="memberUpdateMode"></param>
        /// <param name="memberUpdateDelay"></param>
        private static void SetSectrMemberSettings(SECTR_MemberUpdateMode memberUpdateMode, int memberUpdateDelay)
        {
            SECTR_Member[] members = GameObject.FindObjectsOfType<SECTR_Member>();
            if (members != null)
            {
                foreach (SECTR_Member member in members)
                {
                    member.UpdatePrefSettings();
                }
            }
        }
    }
}