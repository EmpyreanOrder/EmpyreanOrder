#if HDPipeline && UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace ProceduralWorlds.HDRPTOD
{
    public class WeatherFXThunder : WeatherFXInstance, IHDRPWeatherVFX
    {
        public ThunderData m_thunderData;
        public Vector2 m_thunderStrikeTime = new Vector2(5f, 10f);

        private float m_thunderTimer;
        private AudioSource m_thunderSoundFX;
        private HDRPTimeOfDay HDRPTimeOfDay;

        private void Update()
        {
            if (BaseSettings.m_state == WeatherVFXState.InActive)
            {
                return;
            }
            else if (BaseSettings.m_state == WeatherVFXState.FadeIn)
            {
                FadeIn();
            }
            else if (BaseSettings.m_state == WeatherVFXState.FadeOut)
            {
                FadeOut();
            }
            else
            {
                Active();
            }
        }

        public void StartWeatherFX(HDRPTimeOfDayWeatherProfile profile)
        {
            HDRPTimeOfDay = BaseSettings.GetTimeOfDaySystem();
            BaseSettings.SetWeatherProfile(profile);
            BaseSettings.SetDuration(0f);
            BaseSettings.ChangeState(WeatherVFXState.FadeIn);
        }
        public void StopWeatherFX()
        {
            BaseSettings.SetDuration(0f);
            BaseSettings.ChangeState(WeatherVFXState.FadeOut);
        }
        public void SetDuration(float value)
        {
            BaseSettings.SetDuration(value);
        }
        public float GetCurrentDuration()
        {
            return BaseSettings.m_duration;
        }
        public void DestroyVFX()
        {
            foreach (HDRPWeatherVisualEffect visualEffect in VisualEffects)
            {
                visualEffect.m_visualEffect.Stop();
                StartCoroutine(visualEffect.StopVFX());
            }

            StartCoroutine(StopAndDestroyVFX());
        }
        public void DestroyInstantly()
        {
            foreach (HDRPWeatherVisualEffect visualEffect in VisualEffects)
            {
                if (visualEffect != null)
                {
                    BaseSettings.DestroyVisualEffect(visualEffect.m_visualEffect);
                }
            }
            DestroyImmediate(gameObject);
        }
        public List<VisualEffect> CanBeControlledByUnderwater()
        {
            return GetVFXThatUnderwaterCanInteractWith();
        }

        private void FadeIn()
        {
            BaseSettings.IncreaseDurationValue(Time.deltaTime / BaseSettings.GetTransitionDuration());
            if (BaseSettings.CheckIsActive())
            {
                BaseSettings.SetNewCurrentMaxVolume(BaseSettings.GetWeatherEffectVolume());
            }
        }
        private void FadeOut()
        {
            BaseSettings.IncreaseDurationValue(Time.deltaTime / BaseSettings.GetTransitionDuration());
        }
        private void Active()
        {
            m_thunderTimer -= Time.deltaTime;
            if (m_thunderTimer <= 0f)
            {
                m_thunderTimer = BaseSettings.GetRandomValue(m_thunderStrikeTime.x, m_thunderStrikeTime.y);
                if (HDRPTimeOfDay != null)
                {
                    StartCoroutine(PlayThunderVFX(m_thunderData));
                }
            }
        }
        private IEnumerator PlayThunderVFX(ThunderData data)
        {
            while (true)
            {
                if (data.m_thunderStrikeSounds.Count < 1)
                {
                    Debug.LogError("Thunder strike sounds has no audio clips exiting thunder vfx");
                    StopAllCoroutines();
                }
                if (m_thunderSoundFX == null)
                {
                    m_thunderSoundFX = gameObject.AddComponent<AudioSource>();
                }

                m_thunderSoundFX.loop = false;
                int thunderCount = BaseSettings.GetRandomValue(data.m_thunderStrikeCountMinMax.x, data.m_thunderStrikeCountMinMax.y);
                float intensity = data.m_intesity;
                if (thunderCount > 0)
                {
                    for (int i = 0; i < thunderCount; i++)
                    {
                        bool isDay = HDRPTimeOfDay.IsDayTime();
                        if (!isDay)
                        {
                            intensity = data.m_intesity * data.m_nightTimeIntensityMultiplier;
                        }
                
                        HDRPTimeOfDay.OverrideLightSource(data.m_temperature, data.m_thunderLight, intensity, data.m_shadows);
                        yield return new WaitForSeconds(data.m_pauseBetweenStrike);
                    }

                    HDRPTimeOfDay.PlaySoundFX(data.m_thunderStrikeSounds[UnityEngine.Random.Range(0, data.m_thunderStrikeSounds.Count - 1)], m_thunderSoundFX, true, data.m_volume);
                }

                StopAllCoroutines();
            }
        }
        private IEnumerator StopAndDestroyVFX()
        {
            while (true)
            {
                foreach (HDRPWeatherVisualEffect visualEffect in VisualEffects)
                {
                    if (visualEffect != null)
                    {
                        if (visualEffect.ReadyToBeDestroyed())
                        {
                            BaseSettings.DestroyVisualEffect(visualEffect.m_visualEffect);
                        }
                    }
                }

                if (AllVisualEffectsDestroyed())
                {
                    DestroyImmediate(gameObject);
                }

                yield return new WaitForEndOfFrame();
            }
        }
    }
}
#endif