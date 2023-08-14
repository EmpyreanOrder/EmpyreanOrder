using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralWorlds.HDRPTOD
{
    public class HDRPTimeOfDayAPI
    {

#if HDPipeline && UNITY_2021_2_OR_NEWER
        /// <summary>
        /// Gets the time of day system instance in the scene
        /// </summary>
        /// <returns></returns>
        public static HDRPTimeOfDay GetTimeOfDay()
        {
            return HDRPTimeOfDay.Instance;
        }
        /// <summary>
        /// Refreshes the time of day system by processing the time of day code
        /// </summary>
        public static void RefreshTimeOfDay()
        {
            if (IsPresent())
            {
                GetTimeOfDay().ProcessTimeOfDay();
            }
        }
        /// <summary>
        /// Starts the weather effect from the index selected. This is not an instant effect
        /// </summary>
        /// <param name="weatherProfileIndex"></param>
        public static void StartWeather(int weatherProfileIndex)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (weatherProfileIndex <= timeOfDay.WeatherProfiles.Count - 1 && weatherProfileIndex >= 0)
                {
                    timeOfDay.StartWeather(weatherProfileIndex);
                }
            }
        }
        /// <summary>
        /// Stops the current active weather. This is an instant effect
        /// </summary>
        public static void StopWeather()
        {
            if (IsPresent())
            {
                GetTimeOfDay().StopWeather();
            }
        }
        /// <summary>
        /// Returns the time of day system, will return -1 if the time of day system is not present.
        /// </summary>
        /// <returns></returns>
        public static float GetCurrentTime()
        {
            if (IsPresent())
            {
                return GetTimeOfDay().TimeOfDay;
            }
            else
            {
                return -1f;
            }
        }
        /// <summary>
        /// Sets the time of day.
        /// If is0To1 is set to true you must provide a 0-1 value and not a 0-24 value the value will be multiplied by 24.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="is0To1"></param>
        public static void SetCurrentTime(float time, bool is0To1 = false)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (is0To1)
                {
                    timeOfDay.TimeOfDay = Mathf.Clamp(time * 24f, 0f, 24f);
                }
                else
                {
                    timeOfDay.TimeOfDay = Mathf.Clamp(time, 0f, 24f);
                }
            }
        }
        /// <summary>
        /// Sets the direction of the system on the y axis
        /// </summary>
        /// <param name="direction"></param>
        public static void SetDirection(float direction)
        {
            if (IsPresent())
            {
                GetTimeOfDay().DirectionY = direction;
            }
        }
        /// <summary>
        /// Sets if you want to use post processing based on the state bool
        /// </summary>
        /// <param name="state"></param>
        public static void SetPostProcessingState(bool state)
        {
            if (IsPresent())
            {
                GetTimeOfDay().UsePostFX = state;
            }
        }
        /// <summary>
        /// Sets if you want to use ambient audio based on the state bool
        /// </summary>
        /// <param name="state"></param>
        public static void SetAmbientAudioState(bool state)
        {
            if (IsPresent())
            {
                GetTimeOfDay().UseAmbientAudio = state;
            }
        }
        /// <summary>
        /// Sets if you want to use underwater overrides based on the state bool
        /// </summary>
        /// <param name="state"></param>
        /// <param name="mode"></param>
        public static void SetUnderwaterState(bool state, UnderwaterOverrideSystemType mode)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                timeOfDay.TimeOfDayProfile.UnderwaterOverrideData.m_useOverrides = state;
                timeOfDay.TimeOfDayProfile.UnderwaterOverrideData.m_systemType = mode;
            }
        }
        /// <summary>
        /// Sets if you want to use weather system based on the state bool
        /// </summary>
        /// <param name="state"></param>
        public static void SetWeatherState(bool state)
        {
            if (IsPresent())
            {
                GetTimeOfDay().UseWeatherFX = state;
            }
        }
        /// <summary>
        /// Sets the shadow distance multiplier
        /// </summary>
        /// <param name="value"></param>
        public static void SetGlobalShadowMultiplier(float value)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    timeOfDay.TimeOfDayProfile.TimeOfDayData.m_shadowDistanceMultiplier = value;
                    RefreshTimeOfDay();
                }
            }
        }
        /// <summary>
        /// Gets the shadow distance multiplier
        /// </summary>
        /// <returns></returns>
        public static float GetGlobalShadowMultiplier()
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    return timeOfDay.TimeOfDayProfile.TimeOfDayData.m_shadowDistanceMultiplier;
                }
            }

            return 1f;
        }
        /// <summary>
        /// Sets the fog distance multiplier
        /// </summary>
        /// <param name="value"></param>
        public static void SetGlobalFogMultiplier(float value)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    timeOfDay.TimeOfDayProfile.TimeOfDayData.m_globalFogMultiplier = value;
                    RefreshTimeOfDay();
                }
            }
        }
        /// <summary>
        /// Gets the fog distance multiplier
        /// </summary>
        /// <returns></returns>
        public static float GetGlobalFogMultiplier()
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    return timeOfDay.TimeOfDayProfile.TimeOfDayData.m_globalFogMultiplier;
                }
            }

            return 1f;
        }
        /// <summary>
        /// Sets the sun distance multiplier
        /// </summary>
        /// <param name="value"></param>
        public static void SetGlobalSunMultiplier(float value)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    timeOfDay.TimeOfDayProfile.TimeOfDayData.m_globalLightMultiplier = value;
                    RefreshTimeOfDay();
                }
            }
        }
        /// <summary>
        /// Gets the sun distance multiplier
        /// </summary>
        /// <returns></returns>
        public static float GetGlobalSunMultiplier()
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    return timeOfDay.TimeOfDayProfile.TimeOfDayData.m_globalLightMultiplier;
                }
            }

            return 1f;
        }
        /// <summary>
        /// Sets the auto update multiplier
        /// </summary>
        /// <param name="value"></param>
        public static void SetAutoUpdateMultiplier(bool autoUpdate, float value)
        {
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    timeOfDay.m_enableTimeOfDaySystem = autoUpdate;
                    timeOfDay.m_timeOfDayMultiplier = value;
                }
            }
        }
        /// <summary>
        /// Gets the auto update multiplier
        /// </summary>
        /// <returns></returns>
        public static void GetAutoUpdateMultiplier(out bool autoUpdate, out float value)
        {
            autoUpdate = false;
            value = 1f;
            if (IsPresent())
            {
                HDRPTimeOfDay timeOfDay = GetTimeOfDay();
                if (timeOfDay.TimeOfDayProfile != null)
                {
                    autoUpdate = timeOfDay.m_enableTimeOfDaySystem;
                    value = timeOfDay.m_timeOfDayMultiplier;
                }
            }
        }

        /// <summary>
        /// Retrns true if the system is present
        /// </summary>
        /// <returns></returns>
        private static bool IsPresent()
        {
            return HDRPTimeOfDay.Instance;
        }
#endif
    }
}