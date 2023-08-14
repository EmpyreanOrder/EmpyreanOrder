#if HDPipeline && UNITY_2021_2_OR_NEWER
using Gaia.Internal;
using PWCommon5;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ProceduralWorlds.HDRPTOD
{
    [CustomEditor(typeof(HDRPTimeOfDay))]
    public class HDRPTimeOfDayEditor : PWEditor
    {
        private HDRPTimeOfDay m_tod;
        private GUIStyle m_boxStyle;
        private SceneView m_sceneView;
        private static EditorUtils m_editorUtils;

#region Unity Functions

        private void OnEnable()
        {
            m_tod = (HDRPTimeOfDay) target;
            if (m_tod != null)
            {
                m_tod.SetHasBeenSetup(m_tod.SetupHDRPTimeOfDay());
            }

            EditorApplication.update -= SimulateUpdate;
            m_sceneView = SceneView.lastActiveSceneView;

            m_editorUtils = PWApp.GetEditorUtils(this);
        }
        private void OnDestroy()
        {
            if (m_editorUtils != null)
            {
                m_editorUtils.Dispose();
                m_editorUtils = null;
            }
        }
        public override void OnInspectorGUI()
        {
            m_editorUtils.Initialize();
            if (m_tod == null)
            {
                m_tod = (HDRPTimeOfDay) target;
            }

            //Set up the box style
            if (m_boxStyle == null)
            {
                m_boxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = {textColor = GUI.skin.label.normal.textColor},
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }

            EditorGUILayout.HelpBox("This version of HDRP Time Of Day is in PREVIEW", MessageType.Info);
            m_editorUtils.Panel("GlobalPanel", GlobalPanel, true);
            m_editorUtils.Panel("TimeOfDayPanel", TimeOfDayPanel, true);
            m_editorUtils.Panel("PostProcessingPanel", PostProcessingPanel, false);
            m_editorUtils.Panel("AmbientAudioPanel", AmbientAudioPanel, false);
            m_editorUtils.Panel("UnderwaterPanel", UnderwaterPanel, false);
            m_editorUtils.Panel("WeatherPanel", WeatherPanel, false);
            m_editorUtils.Panel("DebugPanel", DebugPanel, false);
        }

#endregion
#region Panels

        private void GlobalPanel(bool helpEnabled)
        {
            EditorGUILayout.BeginVertical(m_boxStyle);
            EditorGUI.BeginChangeCheck();
            m_tod.Player = (Transform)m_editorUtils.ObjectField("PlayerCamera", m_tod.Player, typeof(Transform), true, helpEnabled);
            ReflectionProbeSyncSettings();
            bool useOverrideVolumes = m_tod.UseOverrideVolumes;
            useOverrideVolumes = m_editorUtils.Toggle("UseOverrideVolumes", useOverrideVolumes, helpEnabled);
            if (useOverrideVolumes)
            {
                EditorGUI.indentLevel++;
                m_tod.AutoOrganizeOverrideVolumes = m_editorUtils.Toggle("AutoOrganize", m_tod.AutoOrganizeOverrideVolumes, helpEnabled);
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (!useOverrideVolumes)
                {
                    HDRPTimeOfDayOverrideVolumeController controller = HDRPTimeOfDayOverrideVolumeController.Instance;
                    if (controller != null)
                    {
                        if (EditorUtility.DisplayDialog("Remove Volume Controller",
                                "Would you like to remove the override volume controller?", "Yes", "No"))
                        {
                            DestroyImmediate(controller);
                            m_tod.UseOverrideVolumes = useOverrideVolumes;
                            EditorUtility.SetDirty(m_tod);
                            EditorGUIUtility.ExitGUI();
                        }
                    }
                }

                m_tod.UseOverrideVolumes = useOverrideVolumes;
                EditorUtility.SetDirty(m_tod);
            }
            EditorGUILayout.EndVertical();
        }
        private void TimeOfDayPanel(bool helpEnabled)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginVertical(m_boxStyle);
            m_tod.TimeOfDayProfile = (HDRPTimeOfDayProfile)m_editorUtils.ObjectField("TimeOfDayProfile", m_tod.TimeOfDayProfile, typeof(HDRPTimeOfDayProfile), false, helpEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(m_tod);
                RepaintSceneView();
            }
            if (m_tod.TimeOfDayProfile != null)
            {
                EditorGUI.BeginChangeCheck();
                m_tod.TimeOfDay = m_editorUtils.Slider("TimeOfDay", m_tod.TimeOfDay, 0f, 24f, helpEnabled);
                string direction = "N";
                if (m_tod.DirectionY < 45)
                {
                    direction = "N";
                }
                else if (m_tod.DirectionY >= 45 && m_tod.DirectionY <= 90)
                {
                    direction = "NE";
                }
                else if (m_tod.DirectionY >= 90 && m_tod.DirectionY <= 135)
                {
                    direction = "E";
                }
                else if (m_tod.DirectionY >= 135 && m_tod.DirectionY <= 180)
                {
                    direction = "SE";
                }
                else if (m_tod.DirectionY >= 180 && m_tod.DirectionY <= 225)
                {
                    direction = "S";
                }
                else if (m_tod.DirectionY >= 225 && m_tod.DirectionY <= 270)
                {
                    direction = "SW";
                }
                else if (m_tod.DirectionY >= 270 && m_tod.DirectionY <= 315)
                {
                    direction = "W";
                }
                else
                {
                    direction = "NW";
                }
                m_tod.DirectionY = EditorGUILayout.Slider(m_editorUtils.GetTextValue("Direction") + " (" + direction + ")", m_tod.DirectionY, 0f, 360f);
                m_editorUtils.InlineHelp("Direction", helpEnabled);
                m_tod.m_enableTimeOfDaySystem = m_editorUtils.Toggle("AutoUpdate", m_tod.m_enableTimeOfDaySystem, helpEnabled);
                if (m_tod.m_enableTimeOfDaySystem)
                {
                    EditorGUI.indentLevel++;
                    m_tod.m_timeOfDayMultiplier = m_editorUtils.FloatField("TimeOfDayMultiplier", m_tod.m_timeOfDayMultiplier, helpEnabled);
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(m_tod);
                    RepaintSceneView();
                }

                EditorGUI.BeginChangeCheck();

                //Advanced Lighting
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_advancedLightingSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_advancedLightingSettings, "Advanced Lighting Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_advancedLightingSettings)
                {
                    AdvancedLightingSettings(helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Cloud Settings
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudSettings, "Cloud Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudSettings)
                {
                    GlobalCloudType cloudType = m_tod.TimeOfDayProfile.TimeOfDayData.m_globalCloudType;
                    cloudType = (GlobalCloudType)m_editorUtils.EnumPopup("GlobalCloudType", cloudType, helpEnabled);
                    if (cloudType != m_tod.TimeOfDayProfile.TimeOfDayData.m_globalCloudType)
                    {
                        m_tod.TimeOfDayProfile.TimeOfDayData.m_globalCloudType = cloudType;
                        m_tod.SetupVisualEnvironment();
                    }
                    switch (m_tod.TimeOfDayProfile.TimeOfDayData.m_globalCloudType)
                    {
                        case GlobalCloudType.Volumetric:
                        {
                            VolumetricCloudSettings(helpEnabled);
                            break;
                        }
                        case GlobalCloudType.Procedural:
                        {

                            ProceduralCloudSettings(helpEnabled);
                            break;
                        }
                        case GlobalCloudType.Both:
                        {
                            VolumetricCloudSettings(helpEnabled);
                            ProceduralCloudSettings(helpEnabled);
                            break;
                        }
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Duration Settings
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_durationSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_durationSettings, "Duration Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_durationSettings)
                {
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_dayDuration = m_editorUtils.FloatField("DayDuration", m_tod.TimeOfDayProfile.TimeOfDayData.m_dayDuration, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_nightDuration = m_editorUtils.FloatField("NightDuration", m_tod.TimeOfDayProfile.TimeOfDayData.m_nightDuration, helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Fog Settings
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_fogSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_fogSettings, "Fog Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_fogSettings)
                {
                    FogSettings(helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Sky Settings
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_skySettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_skySettings, "Sky Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_skySettings)
                {
                    SkySettings(helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Sun Settings
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_sunSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.TimeOfDayData.m_sunSettings, "Sun/Moon Settings", true);
                if (m_tod.TimeOfDayProfile.TimeOfDayData.m_sunSettings)
                {
                    SunSettings(helpEnabled);
                    LensFlareSettings(helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                //Ray Tracing
                EditorGUILayout.BeginVertical(m_boxStyle);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSettings, "Ray Tracing Settings", true);
                if (m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSettings)
                {
                    RayTracingSettings(helpEnabled);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(m_tod.TimeOfDayProfile);
                    EditorUtility.SetDirty(m_tod);
                    m_tod.ProcessTimeOfDay();
                    RepaintSceneView();
                }
            }
            EditorGUILayout.EndVertical();
        }
        private void PostProcessingPanel(bool helpEnabled)
        {
            //Post FX
            EditorGUILayout.BeginVertical(m_boxStyle);
            EditorGUI.BeginChangeCheck();
            m_tod.UsePostFX = EditorGUILayout.Toggle("Use Post Processing", m_tod.UsePostFX);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(m_tod);
            }
            if (m_tod.UsePostFX)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.indentLevel++;
                HDRPTimeOfDayPostFXProfile postFXProfile = m_tod.TimeOfDayPostFxProfile;
                postFXProfile = (HDRPTimeOfDayPostFXProfile)EditorGUILayout.ObjectField("Post Processing Profile", postFXProfile, typeof(HDRPTimeOfDayPostFXProfile), false);
                if (postFXProfile != m_tod.TimeOfDayPostFxProfile)
                {
                    m_tod.TimeOfDayPostFxProfile = postFXProfile;
                    m_tod.SetHasBeenSetup(m_tod.SetupHDRPTimeOfDay());
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(m_tod);
                }
                EditorGUI.indentLevel--;
                if (m_tod.TimeOfDayPostFxProfile != null)
                {
                    EditorGUI.BeginChangeCheck();
                    //Ambient Occlusion Settings
                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientOcclusionSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientOcclusionSettings, "Ambient Occlusion Settings", true);
                    if (m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientOcclusionSettings)
                    {
                        AmbientOcclusionSettings(helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    //Bloom Settings
                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomSettings, "Bloom Settings", true);
                    if (m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomSettings)
                    {
                        BloomSettings(helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    //Color Grading Settings
                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_colorGradingSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_colorGradingSettings, "Color Grading Settings", true);
                    if (m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_colorGradingSettings)
                    {
                        ColorGradingSettings(helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    //Shadow Toning Settings
                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadowToningSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadowToningSettings, "Shadow Toning Settings", true);
                    if (m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadowToningSettings)
                    {
                        ShadowToningSettings(helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    //Vignette Settings
                    EditorGUILayout.BeginVertical(m_boxStyle);
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteSettings = EditorGUILayout.Foldout(m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteSettings, "Vignette Settings", true);
                    if (m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteSettings)
                    {
                        VignetteSettings(helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(m_tod.TimeOfDayPostFxProfile);
                        m_tod.ProcessTimeOfDay();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }
        private void AmbientAudioPanel(bool helpEnabled)
        {
            //Ambient
            EditorGUILayout.BeginVertical(m_boxStyle);
            m_tod.UseAmbientAudio = m_editorUtils.Toggle("UseAmbientAudio", m_tod.UseAmbientAudio, helpEnabled);
            if (m_tod.UseAmbientAudio)
            {
                AmbientAudio(helpEnabled);
            }
            EditorGUILayout.EndVertical();
        }
        private void UnderwaterPanel(bool helpEnabled)
        {
            //Underwater
            EditorGUILayout.BeginVertical(m_boxStyle);
            UnderwaterOverridesSettings(helpEnabled);
            EditorGUILayout.EndVertical();
        }
        private void WeatherPanel(bool helpEnabled)
        {
            //Weather
            EditorGUILayout.BeginVertical(m_boxStyle);
            WeatherSettings(helpEnabled);
            EditorGUILayout.EndVertical();
        }
        private void DebugPanel(bool helpEnabled)
        {
            EditorGUILayout.BeginVertical(m_boxStyle);
            DebugSettings(helpEnabled);
            EditorGUILayout.EndVertical();
        }

#endregion
#region Panels Functions

        private void ReflectionProbeSyncSettings()
        {
            m_tod.m_enableReflectionProbeSync = false;
            /*bool enableSync = m_tod.m_enableReflectionProbeSync;
            enableSync = EditorGUILayout.Toggle("Reflection Probe Synchronization", enableSync);
            if (enableSync != m_tod.m_enableReflectionProbeSync)
            {
                m_tod.m_enableReflectionProbeSync = enableSync;
                if (enableSync)
                {
                    if (EditorUtility.DisplayDialog("Probe Sync Enabled",
                        "You have enabled 'Reflection Probe Synchronization' this will refresh any probes that have the script attached to the probes in your scene. Would you like to add the script to all your probes in your scene?",
                        "Yes", "No")) 
                    {
                        ReflectionProbe[] probes = GaiaUtils.FindOOTs<ReflectionProbe>();
                        if (probes.Length > 0)
                        {
                            foreach (ReflectionProbe probe in probes)
                            {
                                if (probe.GetComponent<HDRPTimeOfDayReflectionProbeSync>() == null)
                                {
                                    HDRPTimeOfDayReflectionProbeSync system = probe.gameObject.AddComponent<HDRPTimeOfDayReflectionProbeSync>();
                                    system.Setup();
                                }
                            }
                        }
                        m_tod.UpdateProbeSyncTime(m_tod.ReflectionProbeSyncTime);
                        EditorGUIUtility.ExitGUI();
                    }
                    else
                    {
                        EditorGUIUtility.ExitGUI();
                    }
                }
            }
            if (enableSync)
            {
                EditorGUI.indentLevel++;
                m_tod.ReflectionProbeSyncTime = EditorGUILayout.Slider("Reflection Probe Sync Time", m_tod.ReflectionProbeSyncTime, 0.1f, 20f);
                if (GUILayout.Button("Manual Refresh"))
                {
                    m_tod.RefreshReflectionProbes();
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("Please note that this will sync probes up every time the time of day or direction value has changed and can be very expensive feature. In the future this will be far more optimized when the 'HDRP Time Of Day' system exits the BETA.", MessageType.Warning);
            }*/
        }
        /// <summary>
        /// Debugging
        /// </summary>
        private void DebugSettings(bool helpEnabled)
        {
            EditorGUI.BeginChangeCheck();
            bool roundUp = m_tod.DebugSettings.m_roundUp;
            float simulateSpeed = m_tod.DebugSettings.m_simulationSpeed;
            simulateSpeed = m_editorUtils.FloatField("SimulateSpeed", simulateSpeed, helpEnabled);
            roundUp = m_editorUtils.Toggle("RoundUp", roundUp, helpEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_tod, "ChangedDebugSettings");
                m_tod.DebugSettings.m_roundUp = roundUp;
                m_tod.DebugSettings.m_simulationSpeed = simulateSpeed;
                EditorUtility.SetDirty(m_tod);
            }
            if (m_editorUtils.Button("FetchDebugInformation"))
            {
                m_tod.GetDebugInformation();
            }
            if (m_editorUtils.Button("RefreshTimeOfDayComponents"))
            {
                m_tod.RefreshTimeOfDayComponents();
                if (m_tod.HasBeenSetup())
                {
                    Debug.Log("Components refreshed successfully.");
                    EditorUtility.SetDirty(m_tod);
                }
            }

            if (Application.isPlaying)
            {
                GUI.enabled = false;
            }
            if (m_tod.DebugSettings.m_simulate)
            {
                if (m_editorUtils.Button("StopSimulation"))
                {
                    m_tod.StopSimulate();
                    EditorApplication.update -= SimulateUpdate;
                }
            }
            else
            {
                if (m_editorUtils.Button("StartSimulation"))
                {
                    m_tod.StartSimulate();
                    EditorApplication.update -= SimulateUpdate;
                    EditorApplication.update += SimulateUpdate;
                }
            }
            GUI.enabled = true;

            if (m_editorUtils.Button("RemoveTimeOfDaySystem"))
            {
                HDRPTimeOfDay.RemoveTimeOfDay();
                EditorGUIUtility.ExitGUI();
            }
        }
        /// <summary>
        /// Lighting
        /// </summary>
        private void AdvancedLightingSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSGI = m_editorUtils.Toggle("UseSSGI", m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSGI, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSGI)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_ssgiQuality = (GeneralQuality)m_editorUtils.EnumPopup("SSGIQuality", m_tod.TimeOfDayProfile.TimeOfDayData.m_ssgiQuality, helpEnabled);
                m_tod.TimeOfDayProfile.TimeOfDayData.m_ssgiRenderMode = (SSGIRenderMode)m_editorUtils.EnumPopup("SSGIRenderMode", m_tod.TimeOfDayProfile.TimeOfDayData.m_ssgiRenderMode, helpEnabled);
                EditorGUILayout.HelpBox("SSGI work best when you use reflection probes in your scene so make sure you setup some reflection probes in your scene.", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSR = m_editorUtils.Toggle("UseSSR", m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSR, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useSSR)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_ssrQuality = (GeneralQuality)m_editorUtils.EnumPopup("SSRQuality", m_tod.TimeOfDayProfile.TimeOfDayData.m_ssrQuality, helpEnabled);
                EditorGUI.indentLevel--;
            }
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useContactShadows = m_editorUtils.Toggle("UseContactShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_useContactShadows, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useContactShadows)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_contactShadowsDistance = m_editorUtils.CurveField("ContactShadowsMaxDistance", m_tod.TimeOfDayProfile.TimeOfDayData.m_contactShadowsDistance, helpEnabled);
                EditorGUI.indentLevel--;
            }
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useMicroShadows = m_editorUtils.Toggle("UseMicroShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_useMicroShadows, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_generalExposure = m_editorUtils.CurveField("GeneralExposure", m_tod.TimeOfDayProfile.TimeOfDayData.m_generalExposure, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_ambientIntensity = m_editorUtils.CurveField("AmbientIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_ambientIntensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_ambientReflectionIntensity = m_editorUtils.CurveField("AmbientReflectionIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_ambientReflectionIntensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_planarReflectionIntensity = m_editorUtils.CurveField("PlanarReflectionIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_planarReflectionIntensity, helpEnabled);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Shadow Settings", EditorStyles.boldLabel);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_enableSunShadows = m_editorUtils.Toggle("SunShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_enableSunShadows, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_enableMoonShadows = m_editorUtils.Toggle("MoonShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_enableMoonShadows, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowCascadeCount = m_editorUtils.IntSlider("ShadowCascadeCount", m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowCascadeCount, 1, 4, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowDistanceMultiplier = m_editorUtils.Slider("ShadowDistanceMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowDistanceMultiplier, 0.01f, 5f, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowDistance = m_editorUtils.CurveField("ShadowDistance", m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowDistance, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowTransmissionMultiplier = m_editorUtils.CurveField("TransmissionMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_shadowTransmissionMultiplier, helpEnabled);
        }
        private void SkySettings(bool helpEnabled)
        {
            EditorGUI.BeginChangeCheck();
            m_tod.TimeOfDayProfile.TimeOfDayData.m_horizonOffset = m_editorUtils.Slider("HorizonOffset", m_tod.TimeOfDayProfile.TimeOfDayData.m_horizonOffset, -1f, 1f, helpEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                m_tod.ProcessTimeOfDay();
                EditorUtility.SetDirty(m_tod);
            }
            TimeOfDaySkyMode skyMode = m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMode;
            skyMode = (TimeOfDaySkyMode) m_editorUtils.EnumPopup("SkyMode", skyMode, helpEnabled);
            if (skyMode != m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMode)
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMode = skyMode;
                m_tod.SetupVisualEnvironment();
            }
            switch (m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMode)
            {
                case TimeOfDaySkyMode.Gradient:
                {
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyTopColor = GradientField("TopColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyTopColor, true, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMiddleColor = GradientField("MiddleColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyMiddleColor, true, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyBottomColor = GradientField("BottomColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyBottomColor, true, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyGradientDiffusion = m_editorUtils.CurveField("GradientDiffusion", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyGradientDiffusion, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyExposureGradient = m_editorUtils.CurveField("SkyExposure", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyExposureGradient, helpEnabled);
                    break;
                }
                case TimeOfDaySkyMode.PhysicallyBased:
                {
                    EditorGUI.BeginChangeCheck();
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxExposure = m_editorUtils.FloatField("SkyExposure", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxExposure, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxGroundColor = m_editorUtils.ColorField("SkyGroundColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxGroundColor, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_starsDayIntensity = m_editorUtils.FloatField("StarsDayIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_starsDayIntensity, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_starsNightIntensity = m_editorUtils.FloatField("StarsNightIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_starsNightIntensity, helpEnabled);

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_tod.SetSkySettings(m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxExposure, m_tod.TimeOfDayProfile.TimeOfDayData.m_skyboxGroundColor);
                        m_tod.SetStarsIntensity(m_tod.IsDayTime());
                    }
                    break;
                }
            }
        }
        private void SunSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunIntensity = m_editorUtils.CurveField("SunIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunIntensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunIntensityMultiplier = m_editorUtils.CurveField("SunMoonIntensityMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunIntensityMultiplier, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunTemperature = m_editorUtils.CurveField("SunTemperature", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunTemperature, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunColorFilter = GradientField("SunColorFilter", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunColorFilter, false, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_moonIntensity = m_editorUtils.CurveField("MoonIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_moonIntensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_moonTemperature = m_editorUtils.CurveField("MoonTemperature", m_tod.TimeOfDayProfile.TimeOfDayData.m_moonTemperature, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_moonColorFilter = GradientField("MoonColorFilter", m_tod.TimeOfDayProfile.TimeOfDayData.m_moonColorFilter, false, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunVolumetrics = m_editorUtils.CurveField("SunMoonVolumetric", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunVolumetrics, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_sunVolumetricShadowDimmer = m_editorUtils.CurveField("SunMoonVolumetricDimmer", m_tod.TimeOfDayProfile.TimeOfDayData.m_sunVolumetricShadowDimmer, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_globalLightMultiplier = m_editorUtils.Slider("GlobalLightIntensityMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_globalLightMultiplier, 0.001f, 5f, helpEnabled);
        }
        private void FogSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayProfile.TimeOfDayData.m_fogQuality = (GeneralQuality)m_editorUtils.EnumPopup("FogQuality", m_tod.TimeOfDayProfile.TimeOfDayData.m_fogQuality, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useDenoising = m_editorUtils.Toggle("UseDenoising", m_tod.TimeOfDayProfile.TimeOfDayData.m_useDenoising, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useDenoising)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_denoisingQuality = (GeneralQuality)m_editorUtils.EnumPopup("DenoisingQuality", m_tod.TimeOfDayProfile.TimeOfDayData.m_denoisingQuality, helpEnabled);
                EditorGUI.indentLevel--;
            }
            m_tod.TimeOfDayProfile.TimeOfDayData.m_fogColor = GradientField("FogColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_fogColor, false, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_fogDistance = m_editorUtils.CurveField("FogDistance", m_tod.TimeOfDayProfile.TimeOfDayData.m_fogDistance, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_fogHeight = m_editorUtils.CurveField("FogHeight", m_tod.TimeOfDayProfile.TimeOfDayData.m_fogHeight, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_fogDensity = m_editorUtils.CurveField("LocalFogDensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_fogDensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_localFogMultiplier = m_editorUtils.CurveField("LocalFogMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_localFogMultiplier, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_globalFogMultiplier = m_editorUtils.Slider("GlobalFogMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_globalFogMultiplier, 0.001f, 15f, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogDistance = m_editorUtils.CurveField("VolumetricDistance", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogDistance, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogAnisotropy = m_editorUtils.CurveField("VolumetricAnisotropy", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogAnisotropy, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogSliceDistributionUniformity = m_editorUtils.CurveField("SliceDistributionUniformity", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricFogSliceDistributionUniformity, helpEnabled);
        }
        private void LensFlareSettings(bool helpEnabled)
        {
            //Sun
            EditorGUI.BeginChangeCheck();
            TimeOfDayLensFlareProfile sunLensFlare = m_tod.TimeOfDayProfile.TimeOfDayData.m_sunLensFlareProfile;
            TimeOfDayLensFlareProfile moonLensFlare = m_tod.TimeOfDayProfile.TimeOfDayData.m_moonLensFlareProfile;
            EditorGUILayout.LabelField("Sun Lens Flare Settings", EditorStyles.boldLabel);
            if (sunLensFlare.m_useLensFlare || moonLensFlare.m_useLensFlare)
            {
                SunFlareInfoHelp();
            }
            EditorGUI.indentLevel++;
            sunLensFlare.m_useLensFlare = m_editorUtils.Toggle("UseSunLensFlare", sunLensFlare.m_useLensFlare, helpEnabled);
            if (sunLensFlare.m_useLensFlare)
            {
                sunLensFlare.m_lensFlareData = (LensFlareDataSRP)m_editorUtils.ObjectField("LensFlareData", sunLensFlare.m_lensFlareData, typeof(LensFlareDataSRP), false, helpEnabled);
                sunLensFlare.m_intensity = m_editorUtils.CurveField("SunFlareIntensity", sunLensFlare.m_intensity, helpEnabled);
                sunLensFlare.m_scale = m_editorUtils.CurveField("SunFlareScale", sunLensFlare.m_scale, helpEnabled);
                sunLensFlare.m_enableOcclusion = m_editorUtils.Toggle("SunFlareEnableOcclusion", sunLensFlare.m_enableOcclusion, helpEnabled);
                if (sunLensFlare.m_enableOcclusion)
                {
                    EditorGUI.indentLevel++;
                    sunLensFlare.m_occlusionRadius = m_editorUtils.FloatField("SunFlareRadius", sunLensFlare.m_occlusionRadius, helpEnabled);
                    sunLensFlare.m_sampleCount = m_editorUtils.IntSlider("SunFlareSampleCount", sunLensFlare.m_sampleCount, 1, 64, helpEnabled);
                    sunLensFlare.m_occlusionOffset = m_editorUtils.FloatField("SunFlareOffset", sunLensFlare.m_occlusionOffset, helpEnabled);
                    sunLensFlare.m_allowOffScreen = m_editorUtils.Toggle("SunFlareAllowOffScreen", sunLensFlare.m_allowOffScreen, helpEnabled);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_sunLensFlareProfile = sunLensFlare;
            }

            //Moon
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Moon Lens Flare Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            moonLensFlare.m_useLensFlare = m_editorUtils.Toggle("UseMoonLensFlare", moonLensFlare.m_useLensFlare, helpEnabled);
            if (moonLensFlare.m_useLensFlare)
            {
                moonLensFlare.m_lensFlareData = (LensFlareDataSRP)m_editorUtils.ObjectField("LensFlareData", moonLensFlare.m_lensFlareData, typeof(LensFlareDataSRP), false);
                moonLensFlare.m_intensity = m_editorUtils.CurveField("SunFlareIntensity", moonLensFlare.m_intensity, helpEnabled);
                moonLensFlare.m_scale = m_editorUtils.CurveField("SunFlareScale", moonLensFlare.m_scale);
                moonLensFlare.m_enableOcclusion = m_editorUtils.Toggle("SunFlareEnableOcclusion", moonLensFlare.m_enableOcclusion, helpEnabled);
                if (moonLensFlare.m_enableOcclusion)
                {
                    EditorGUI.indentLevel++;
                    moonLensFlare.m_occlusionRadius = m_editorUtils.FloatField("SunFlareRadius", moonLensFlare.m_occlusionRadius, helpEnabled);
                    moonLensFlare.m_sampleCount = m_editorUtils.IntSlider("SunFlareSampleCount", moonLensFlare.m_sampleCount, 1, 64, helpEnabled);
                    moonLensFlare.m_occlusionOffset = m_editorUtils.FloatField("SunFlareOffset", moonLensFlare.m_occlusionOffset, helpEnabled);
                    moonLensFlare.m_allowOffScreen = m_editorUtils.Toggle("SunFlareAllowOffScreen", moonLensFlare.m_allowOffScreen, helpEnabled);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_moonLensFlareProfile = moonLensFlare;
            }
        }
        private void VolumetricCloudSettings(bool helpEnabled)
        {
            EditorGUILayout.LabelField("Volumetric Cloud Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useLocalClouds = m_editorUtils.Toggle("UseLocalClouds", m_tod.TimeOfDayProfile.TimeOfDayData.m_useLocalClouds, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useLocalClouds)
            {
                EditorGUILayout.HelpBox("Local Clouds is enabled, you need to have a high far clip plane value to use this feature. Recommend a min value of 15000 for the far clip plane on the camera.", MessageType.Info);
            }
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudPresets = (VolumetricClouds.CloudPresets)m_editorUtils.EnumPopup("CloudPreset", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudPresets, helpEnabled);
            switch (m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudPresets)
            {
                case VolumetricClouds.CloudPresets.Custom:
                {
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_erosionNoiseType = (VolumetricClouds.CloudErosionNoise)m_editorUtils.EnumPopup("ErosionNoiseType", m_tod.TimeOfDayProfile.TimeOfDayData.m_erosionNoiseType, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricDensityMultiplier = m_editorUtils.CurveField("DensityMultiplier", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricDensityMultiplier, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricDensityCurve = m_editorUtils.CurveField("CustomDensityCurve", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricDensityCurve, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricShapeFactor = m_editorUtils.CurveField("ShapeFactor", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricShapeFactor, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricShapeScale = m_editorUtils.CurveField("ShapeScale", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricShapeScale, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionFactor = m_editorUtils.CurveField("ErosionFactor", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionFactor, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionScale = m_editorUtils.CurveField("ErosionScale", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionScale, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionCurve = m_editorUtils.CurveField("CustomErosionCurve", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionCurve, helpEnabled);
                    m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricAmbientOcclusionCurve = m_editorUtils.CurveField("CustomAmbientOcclusionCurve", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricAmbientOcclusionCurve, helpEnabled);
                    break;
                }
            }

            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricLowestCloudAltitude = m_editorUtils.CurveField("LowestCloudAltitude", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricLowestCloudAltitude, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudThickness = m_editorUtils.CurveField("CloudThickness", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudThickness, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindDirection = m_editorUtils.CurveField("CloudWindDirection", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindDirection, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindSpeed = m_editorUtils.CurveField("CloudWindSpeed", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindSpeed, helpEnabled);
            //Lighting
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricAmbientLightProbeDimmer = m_editorUtils.CurveField("AmbientLightProbeDimmer", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricAmbientLightProbeDimmer, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricSunLightDimmer = m_editorUtils.CurveField("SunLightDimmer", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricSunLightDimmer, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionOcclusion = m_editorUtils.CurveField("ErosionOcclusion", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricErosionOcclusion, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricScatteringTint = GradientField("ScatteringTint", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricScatteringTint, false, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricPowderEffectIntensity = m_editorUtils.CurveField("PowderEffectIntensity", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricPowderEffectIntensity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricMultiScattering = m_editorUtils.CurveField("MultiScattering", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricMultiScattering, helpEnabled);
            //Shadows
            m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadows = m_editorUtils.Toggle("EnableShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadows, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadows)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadowResolution = (VolumetricClouds.CloudShadowResolution)m_editorUtils.EnumPopup("ShadowResolution", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadowResolution, helpEnabled); 
                m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadowOpacity = m_editorUtils.CurveField("ShadowOpacity", m_tod.TimeOfDayProfile.TimeOfDayData.m_volumetricCloudShadowOpacity, helpEnabled);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }
        private void ProceduralCloudSettings(bool helpEnabled)
        {
            EditorGUILayout.LabelField("Procedual Cloud Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Using procedural clouds might causes graphical issues due to a rendering queue issue within Unity HDRP core code that has the clouds rendering over the top of opaque object.", MessageType.Warning);
            EditorGUI.indentLevel++;
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayers = (CloudLayerType) m_editorUtils.EnumPopup("CloudLayersType", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayers, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudResolution = (CloudResolution) m_editorUtils.EnumPopup("CloudResolution", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudResolution, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudOpacity = m_editorUtils.CurveField("CloudOpacity", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudOpacity, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerAChannel = (CloudLayerChannelMode)m_editorUtils.EnumPopup("CloudLayerAChannelMode", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerAChannel, helpEnabled);
            EditorGUI.indentLevel++;
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerAOpacityR = m_editorUtils.CurveField("CloudLayerAOpacity", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerAOpacityR, helpEnabled);
            EditorGUI.indentLevel--;
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayers == CloudLayerType.Double)
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerBChannel = (CloudLayerChannelMode)m_editorUtils.EnumPopup("CloudLayerBChannelMode", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerBChannel, helpEnabled);
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerBOpacityR = m_editorUtils.CurveField("CloudLayerBOpacity", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLayerBOpacityR, helpEnabled);
                EditorGUI.indentLevel--;
            }

            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudTintColor = GradientField("TintColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudTintColor, false, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudExposure = m_editorUtils.CurveField("CloudExposure", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudExposure, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_globalCloudType == GlobalCloudType.Procedural)
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindDirection = m_editorUtils.CurveField("CloudWindDirection", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindDirection, helpEnabled);
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindSpeed = m_editorUtils.CurveField("CloudWindSpeed", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudWindSpeed, helpEnabled);
            }

            m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLighting = m_editorUtils.Toggle("UseCloudLighting", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudLighting, helpEnabled);
            m_tod.TimeOfDayProfile.TimeOfDayData.m_useCloudShadows = m_editorUtils.Toggle("UseCloudShadows", m_tod.TimeOfDayProfile.TimeOfDayData.m_useCloudShadows, helpEnabled);
            if (m_tod.TimeOfDayProfile.TimeOfDayData.m_useCloudShadows)
            {
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudShadowOpacity = m_editorUtils.CurveField("CloudShadowOpacity", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudShadowOpacity, helpEnabled);
                m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudShadowColor = GradientField("CloudShadowColor", m_tod.TimeOfDayProfile.TimeOfDayData.m_cloudShadowColor, false, helpEnabled);
            }
            EditorGUI.indentLevel--;
        }
        private void WeatherSettings(bool helpEnabled)
        {
            EditorGUI.BeginChangeCheck();
            bool useWeather = m_tod.UseWeatherFX;
            useWeather = m_editorUtils.Toggle("UseWeatherFX", useWeather, helpEnabled);
            if (useWeather != m_tod.UseWeatherFX)
            {
                m_tod.UseWeatherFX = useWeather;
                if (useWeather)
                {
                    EditorGUIUtility.ExitGUI();
                }
            }
            if (useWeather)
            {
                EditorGUI.indentLevel++;
                m_tod.m_avoidSameRandomWeather = m_editorUtils.Toggle("AvoidSameWeatherProfile", m_tod.m_avoidSameRandomWeather, helpEnabled);
                m_tod.m_resetWeatherShaderProperty = m_editorUtils.Toggle("ResetShaderProperties", m_tod.m_resetWeatherShaderProperty, helpEnabled);
                m_tod.m_randomWeatherTimer = m_editorUtils.Vector2Field("WeatherMinMaxWaitTime", m_tod.m_randomWeatherTimer, helpEnabled);
                if (m_tod.WeatherProfiles.Count > 0)
                {
                    for (int i = 0; i < m_tod.WeatherProfiles.Count; i++)
                    {
                        EditorGUILayout.BeginVertical(m_boxStyle);
                        EditorGUILayout.BeginHorizontal();
                        if (m_tod.WeatherProfiles[i] == null)
                        {
                            m_tod.WeatherProfiles[i] = (HDRPTimeOfDayWeatherProfile)EditorGUILayout.ObjectField("NoProfileSpecified", m_tod.WeatherProfiles[i], typeof(HDRPTimeOfDayWeatherProfile), false);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(m_tod.WeatherProfiles[i].WeatherData.m_weatherName))
                            {
                                m_tod.WeatherProfiles[i] = (HDRPTimeOfDayWeatherProfile)EditorGUILayout.ObjectField("No Name Specified", m_tod.WeatherProfiles[i], typeof(HDRPTimeOfDayWeatherProfile), false);
                            }
                            else
                            {
                                m_tod.WeatherProfiles[i] = (HDRPTimeOfDayWeatherProfile)EditorGUILayout.ObjectField("Profile: " + m_tod.WeatherProfiles[i].WeatherData.m_weatherName, m_tod.WeatherProfiles[i], typeof(HDRPTimeOfDayWeatherProfile), false);
                            }
                        }
                        if (m_editorUtils.Button("Remove", GUILayout.MaxWidth(80f)))
                        {
                            m_tod.WeatherProfiles.RemoveAt(i);
                            EditorUtility.SetDirty(m_tod);
                            EditorGUIUtility.ExitGUI();
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        if (!Application.isPlaying)
                        {
                            GUI.enabled = false;
                        }
                        else
                        {
                            if (m_tod.WeatherActive())
                            {
                                GUI.enabled = false;
                            }
                        }

                        if (m_editorUtils.Button("StartWeather"))
                        {
                            m_tod.StartWeather(i);
                        }

                        if (Application.isPlaying)
                        {
                            GUI.enabled = true;
                        }

                        if (m_tod.WeatherActive())
                        {
                            GUI.enabled = true;
                        }
                        else
                        {
                            GUI.enabled = false;
                        }

                        if (m_editorUtils.Button("StopWeather"))
                        {
                            m_tod.StopWeather();
                        }

                        GUI.enabled = true;

                        EditorGUILayout.EndHorizontal();

                        if (m_editorUtils.Button("CopySettingsOver"))
                        {
                            TimeOfDayProfileData.CopySettings(m_tod.WeatherProfiles[i].WeatherData.m_weatherData, m_tod.TimeOfDayProfile.TimeOfDayData);
                            EditorUtility.SetDirty(m_tod.WeatherProfiles[i]);
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                m_editorUtils.InlineHelp("WeatherProfile", helpEnabled);
                if (m_editorUtils.Button("AddNewWeatherProfile"))
                {
                    m_tod.WeatherProfiles.Add(null);
                }
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(m_tod);
            }
        }
        private void SunFlareInfoHelp()
        {
            EditorGUILayout.HelpBox("Please note that Unity sun flare in HDRP does not yet get culled by volumetric clouds and they will render through the clouds. We hope Unity will add support for this in the future, if this notice is removed then unity has added support.", MessageType.Info);
        }
        /// <summary>
        /// Post Processing
        /// </summary>
        private void AmbientOcclusionSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientIntensity = m_editorUtils.CurveField("AOIntensity", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientIntensity, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientDirectStrength = m_editorUtils.CurveField("AODirectLightIntensity", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientDirectStrength, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientRadius = m_editorUtils.CurveField("AORadius", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_ambientRadius, helpEnabled);
        }
        private void ColorGradingSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_contrast = m_editorUtils.CurveField("CGContrast", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_contrast, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_colorFilter = GradientField("CGColorFilter", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_colorFilter, true, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_saturation = m_editorUtils.CurveField("CGSaturation", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_saturation, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_temperature = m_editorUtils.CurveField("CGTemperature", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_temperature, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_tint = m_editorUtils.CurveField("CGTint", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_tint, helpEnabled);
        }
        private void BloomSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomThreshold = m_editorUtils.CurveField("BloomThreshold", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomThreshold, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomIntensity = m_editorUtils.CurveField("BloomIntensity", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomIntensity, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomScatter = m_editorUtils.CurveField("BloomScatter", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomScatter, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomTint = GradientField("BloomTint", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_bloomTint, false, helpEnabled);
        }
        private void ShadowToningSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadows = GradientField("STShadows", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadows, false, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_highlights = GradientField("STHighlights", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_highlights, false, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadowBalance = m_editorUtils.CurveField("STBalance", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_shadowBalance, helpEnabled);
        }
        private void VignetteSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteColor = GradientField("VignetteColor", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteColor, false, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteIntensity = m_editorUtils.CurveField("VignetteIntensity", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteIntensity, helpEnabled);
            m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteSmoothness = m_editorUtils.CurveField("VignetteSmoothness", m_tod.TimeOfDayPostFxProfile.TimeOfDayPostFXData.m_vignetteSmoothness, helpEnabled);
        }
        private void RayTracingSettings(bool helpEnabled)
        {
            //EditorGUILayout.HelpBox("Ray Tracing (Alpha, Preview). Ray tracing is still in development by Unity and some features may not work or may have rendering issues in Unity or in exe builds. To use ray tracing please enable it in the HDRP Asset file and also install DX12, see unity documentation for help on installing ray tracing in your project.", MessageType.Warning);

            m_editorUtils.InlineHelp("RayTracingHelp", true);

            EditorGUI.BeginChangeCheck();
            bool useRayTracing = m_tod.UseRayTracing;
            useRayTracing = m_editorUtils.Toggle("UseRayTracing", useRayTracing, helpEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                if (useRayTracing != m_tod.UseRayTracing)
                {
                    m_tod.UseRayTracing = useRayTracing;
                    if (useRayTracing)
                    {
                        SetScriptDefine("RAY_TRACING_ENABLED", true);
                    }
                    else
                    {
                        SetScriptDefine("RAY_TRACING_ENABLED", false);
                    }
                }
                EditorUtility.SetDirty(m_tod);
            }
            if (useRayTracing)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.indentLevel++;
                //SSGI
                m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSGI = m_editorUtils.Toggle("RayTraceSSGI", m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSGI, helpEnabled);
                if (m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSGI)
                {
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayProfile.RayTracingSettings.m_ssgiRenderMode = (GeneralRenderMode)m_editorUtils.EnumPopup("RenderMode", m_tod.TimeOfDayProfile.RayTracingSettings.m_ssgiRenderMode, helpEnabled);
                    if (m_tod.TimeOfDayProfile.RayTracingSettings.m_ssgiRenderMode == GeneralRenderMode.Performance)
                    {
                        m_tod.TimeOfDayProfile.RayTracingSettings.m_ssgiQuality = (GeneralQuality)m_editorUtils.EnumPopup("Quality", m_tod.TimeOfDayProfile.RayTracingSettings.m_ssgiQuality, helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                }
                //SSR
                m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSR = m_editorUtils.Toggle("RayTraceSSR", m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSR, helpEnabled);
                if (m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSSR)
                {
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayProfile.RayTracingSettings.m_ssrRenderMode = (GeneralRenderMode)m_editorUtils.EnumPopup("RenderMode", m_tod.TimeOfDayProfile.RayTracingSettings.m_ssrRenderMode, helpEnabled);
                    if (m_tod.TimeOfDayProfile.RayTracingSettings.m_ssrRenderMode == GeneralRenderMode.Performance)
                    {
                        m_tod.TimeOfDayProfile.RayTracingSettings.m_ssrQuality = (GeneralQuality)m_editorUtils.EnumPopup("Quality", m_tod.TimeOfDayProfile.RayTracingSettings.m_ssrQuality, helpEnabled);
                    }
                    EditorGUI.indentLevel--;
                }
                //AO
                m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceAmbientOcclusion = m_editorUtils.Toggle("RayTraceAmbientOcclusion", m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceAmbientOcclusion, helpEnabled);
                if (m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceAmbientOcclusion)
                {
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayProfile.RayTracingSettings.m_aoQuality = (GeneralQuality)m_editorUtils.EnumPopup("Quality", m_tod.TimeOfDayProfile.RayTracingSettings.m_aoQuality, helpEnabled);
                    EditorGUI.indentLevel--;
                }
                //Recursive Rendering
                m_tod.TimeOfDayProfile.RayTracingSettings.m_recursiveRendering = m_editorUtils.Toggle("RecursiveRendering", m_tod.TimeOfDayProfile.RayTracingSettings.m_recursiveRendering, helpEnabled);
                //SSS
                m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSubSurfaceScattering = m_editorUtils.Toggle("RayTraceSubSurfaceScattering", m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSubSurfaceScattering, helpEnabled);
                if (m_tod.TimeOfDayProfile.RayTracingSettings.m_rayTraceSubSurfaceScattering)
                {
                    EditorGUI.indentLevel++;
                    m_tod.TimeOfDayProfile.RayTracingSettings.m_subSurfaceScatteringSampleCount = m_editorUtils.IntSlider("SSSampleCount", m_tod.TimeOfDayProfile.RayTracingSettings.m_subSurfaceScatteringSampleCount, 1, 32, helpEnabled);
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(m_tod.TimeOfDayProfile);
                    m_tod.ApplyRayTracingSettings();
                }
            }
        }
        private void SetScriptDefine(string define, bool add)
        {
            bool updateScripting = false;
            var symbols = HDRPTimeOfDayUtils.GetCurrentScriptingDefines();
            
            if (add)
            {
                if (!symbols.Contains(define))
                {
                    updateScripting = true;
                    if (symbols.Length < 1)
                    {
                        symbols += define;
                    }
                    else
                    {
                        symbols += ";" + define;
                    }
                }
            }
            else
            {
                if (symbols.Contains(define))
                {
                    updateScripting = true;
                    symbols = symbols.Replace(define, "");
                }
            }

            if (updateScripting)
            {
                HDRPTimeOfDayUtils.SetCurrentScriptingDefines(symbols);
            }
        }
        /// <summary>
        /// Ambient Audio
        /// </summary>
        private void AmbientAudio(bool helpEnabled)
        {
            EditorGUI.BeginChangeCheck();
            m_tod.AudioProfile = (HDRPTimeOfDayAmbientProfile)m_editorUtils.ObjectField("AmbientProfile", m_tod.AudioProfile, typeof(HDRPTimeOfDayAmbientProfile), false, helpEnabled);
            if (m_tod.AudioProfile != null)
            {
                m_tod.AudioProfile.m_morningAmbient = (AudioClip)m_editorUtils.ObjectField("MorningAmbient", m_tod.AudioProfile.m_morningAmbient, typeof(AudioClip), false, helpEnabled);
                m_tod.AudioProfile.m_afternoonAmbient = (AudioClip)m_editorUtils.ObjectField("AfternoonAmbient", m_tod.AudioProfile.m_afternoonAmbient, typeof(AudioClip), false, helpEnabled);
                m_tod.AudioProfile.m_eveningAmbient = (AudioClip)m_editorUtils.ObjectField("EveningAmbient", m_tod.AudioProfile.m_eveningAmbient, typeof(AudioClip), false, helpEnabled);
                m_tod.AudioProfile.m_nightAmbient = (AudioClip)m_editorUtils.ObjectField("NightAmbient", m_tod.AudioProfile.m_nightAmbient, typeof(AudioClip), false, helpEnabled);
                m_tod.AudioProfile.m_masterVolume = m_editorUtils.Slider("MaxVolume", m_tod.AudioProfile.m_masterVolume, 0f, 1f, helpEnabled);
                m_tod.AudioProfile.m_weatherVolumeMultiplier = m_editorUtils.Slider("WeatherVolumeMultiplier", m_tod.AudioProfile.m_weatherVolumeMultiplier, 0f, 1f, helpEnabled);
                m_tod.AudioProfile.m_timeOfDayIntervals = m_editorUtils.Vector4Field("TimeOfDayIntervals", m_tod.AudioProfile.m_timeOfDayIntervals, helpEnabled);
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (m_tod.AudioProfile != null)
                {
                    EditorUtility.SetDirty(m_tod.AudioProfile);
                }
            }
        }
        /// <summary>
        /// Underwater
        /// </summary>
        private void UnderwaterOverridesSettings(bool helpEnabled)
        {
            m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_useOverrides = m_editorUtils.Toggle("UseUnderwaterOverrides", m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_useOverrides, helpEnabled);
            if (m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_useOverrides)
            {
                EditorGUI.indentLevel++;
                m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_systemType = (UnderwaterOverrideSystemType)m_editorUtils.EnumPopup("SystemSyncType", m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_systemType, helpEnabled);
                m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_underwaterFogColor = GradientField("UnderwaterFogColor", m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_underwaterFogColor, false, helpEnabled);
                m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_underwaterFogColorMultiplier = m_editorUtils.CurveField("UnderwaterFogColorMultiplier", m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_underwaterFogColorMultiplier, helpEnabled);
                m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_multiplyColor = m_editorUtils.ColorField("UnderwaterMultiplyColor", m_tod.TimeOfDayProfile.UnderwaterOverrideData.m_multiplyColor, helpEnabled);
                EditorGUI.indentLevel--;
            }
        }

#endregion
#region Utils

        private void RepaintSceneView()
        {
            if (m_sceneView == null)
            {
                m_sceneView = SceneView.lastActiveSceneView;
            }

            if (m_sceneView != null)
            {
                m_sceneView.Repaint();
            }
        }
        private void SimulateUpdate()
        {
            if (m_tod == null || !m_tod.DebugSettings.m_simulate)
            {
                return;
            }

            m_tod.TimeOfDay += Time.deltaTime * m_tod.DebugSettings.m_simulationSpeed;
            if (m_tod.TimeOfDay >= 24f)
            {
                m_tod.TimeOfDay = 0f;
            }
        }
        /// <summary>
        /// Custom gradient field
        /// </summary>
        /// <param name="key"></param>
        /// <param name="gradient"></param>
        /// <param name="hdr"></param>
        /// <param name="helpEnabled"></param>
        /// <returns></returns>
        private Gradient GradientField(string key, Gradient gradient, bool hdr, bool helpEnabled)
        {
            gradient = EditorGUILayout.GradientField(new GUIContent(m_editorUtils.GetTextValue(key), m_editorUtils.GetTooltip(key)), gradient, true);
            m_editorUtils.InlineHelp(key, helpEnabled);
            return gradient;
        }

#endregion
#region Menu Items

        [MenuItem("Window/Procedural Worlds/HDRP Time Of Day/Add HDRP TOD")]
        public static void AddHDRPTODToScene()
        {
            HDRPTimeOfDay.CreateTimeOfDay(null);
        }
        [MenuItem("Window/Procedural Worlds/HDRP Time Of Day/Remove HDRP TOD")]
        public static void RemoveHDRPTODToScene()
        {
            HDRPTimeOfDay.RemoveTimeOfDay();
        }
        [MenuItem("Assets/Create/Procedural Worlds/HDRP Time Of Day/Create Empty Post FX Profile")]
        public static void CreateHDRPTimeOfDayPostFXProfile()
        {
            HDRPTimeOfDayPostFXProfile asset = ScriptableObject.CreateInstance<HDRPTimeOfDayPostFXProfile>();

            AssetDatabase.CreateAsset(asset, "Assets/HDRP Time Of Day Post FX Profile.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }
        [MenuItem("Assets/Create/Procedural Worlds/HDRP Time Of Day/Create Empty Profile")]
        public static void CreateHDRPTimeOfDayProfile()
        {
            HDRPTimeOfDayProfile asset = ScriptableObject.CreateInstance<HDRPTimeOfDayProfile>();

            AssetDatabase.CreateAsset(asset, "Assets/HDRP Time Of Day Profile.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }
        [MenuItem("Assets/Create/Procedural Worlds/HDRP Time Of Day/Create Empty Weather Profile")]
        public static void CreateHDRPTimeOfDayWeatherProfile()
        {
            HDRPTimeOfDayWeatherProfile asset = ScriptableObject.CreateInstance<HDRPTimeOfDayWeatherProfile>();

            AssetDatabase.CreateAsset(asset, "Assets/HDRP Time Of Day Weather Profile.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }
        [MenuItem("Assets/Create/Procedural Worlds/HDRP Time Of Day/Create Empty Audio Profile")]
        public static void CreateHDRPTimeOfDayAmbientProfile()
        {
            HDRPTimeOfDayAmbientProfile asset = ScriptableObject.CreateInstance<HDRPTimeOfDayAmbientProfile>();

            AssetDatabase.CreateAsset(asset, "Assets/HDRP Time Of Day Ambient Profile.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }
        [MenuItem("Assets/Create/Procedural Worlds/HDRP Time Of Day/Create Empty Defaults Profile")]
        public static void CreateHDRPTimeOfDayDefaultsProfile()
        {
            HDRPDefaultsProfile asset = ScriptableObject.CreateInstance<HDRPDefaultsProfile>();

            AssetDatabase.CreateAsset(asset, "Assets/HDRP Time Of Day Defaults Profile.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }

#endregion
    }
}
#endif