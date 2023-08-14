using UnityEngine;

namespace ProceduralWorlds.HDRPTOD
{
    public class HDRPDefaultsProfile : ScriptableObject
    {
#if HDPipeline && UNITY_2021_2_OR_NEWER
        public HDRPTimeOfDayProfile m_timeOfDayProfile;
        public HDRPTimeOfDayPostFXProfile m_postProcessingProfile;
        public HDRPTimeOfDayAmbientProfile m_ambientAudioProfile;
        [Range(0f, 24f)]
        public float m_startingTimeOfDay = 8f;
        [Range(0f, 360f)]
        public float m_startingDirection = 20f;
        [Range(-1f, 1f)]
        public float m_startingHorizonOffset = 0.1f;
        public Vector2 m_minMaxWeatherWaitTime = new Vector2(120f, 300f);

        /// <summary>
        /// Applies the defaults to the time of day
        /// </summary>
        /// <param name="timeOfDay"></param>
        public void ApplyDefaultsToTimeOfDay(HDRPTimeOfDay timeOfDay)
        {
            if (timeOfDay != null)
            {
                if (m_timeOfDayProfile != null)
                {
                    timeOfDay.TimeOfDayProfile = m_timeOfDayProfile;
                }

                if (m_postProcessingProfile != null)
                {
                    timeOfDay.TimeOfDayPostFxProfile = m_postProcessingProfile;
                }

                if (m_ambientAudioProfile != null)
                {
                    timeOfDay.AudioProfile = m_ambientAudioProfile;
                }

                timeOfDay.TimeOfDay = m_startingTimeOfDay;
                timeOfDay.DirectionY = m_startingDirection;
                timeOfDay.TimeOfDayProfile.TimeOfDayData.m_horizonOffset = m_startingHorizonOffset;
                timeOfDay.m_randomWeatherTimer = m_minMaxWeatherWaitTime;
            }
        }
#endif
    }
}