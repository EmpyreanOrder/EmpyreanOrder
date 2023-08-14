// Copyright (c) 2014 Make Code Now! LLC
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define AUDIO_HUD
#endif

#define RPC_SUPPORTED

#if RPC_SUPPORTED
using System.Reflection;
#endif

using UnityEngine;


#if UNITY_EDITOR
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#elif UNITY_2018_3_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEditor;
#endif
using System.Collections.Generic;

/// \ingroup Audio
/// The beating heart of SECTR_Audio, SECTR_AudioSystem provides all of
/// the services necessary to play sounds and music, control the mix, etc.
/// 
/// The most fundamental service AudioSystem provides is the ability to play
/// sounds. Under the hood, the AudioSystem uses standard Unity AudioSources
/// to play sounds, but layers on a number of signficant optimizations including
/// object pooling, pre-culling of one shots, virtual instances of distant looping objects,
/// and more. In aggregate, they provide a feature rich, but very high performance,
/// solution for playing audio in Unity.
/// 
/// AudioSystem also manages the bus hierarchy, which can be used by designers to
/// mix the game, and by programmers to dynamically modify volumes in response to
/// user input or in-game events. Each AudioSystem instance must have a SECTR_AudioBus
/// assigned to its MasterBus attribute, but it does not need to be the same Bus resource
/// for every scene in the game. If desired, a game may have different bus hierarchies for
/// different parts of the game.
/// 
/// Another useful service provided by the AudioSystem is the management and playback of
/// SECTR_AudioEnvironments. AudioEnvironments are a powerful tool for establishing the basic
/// sonic character of a part of the game world. While the AudioSystem only allows one 
/// AudioEnvironment to be active at a time, they are stored in a stack (where the topmost element
/// is the highest priority cue. This interface allows clients (usually trigger volume type objects)
/// to overlap and even be nested within one another, allowing sound designers to create rich,
/// layered sonic spaces.
/// 
/// Lastly, the AudioSystem provides a simple interface for playing Music. Music in this case is simply
/// a looping, 2D cue, but the system will ensure that there is only one "music" cue every playing at once
/// (aside from cross fades between Cues). This simple concept maps well to the music implementations of most games,
/// especially when combined with the playback options in SECTR_AudioCue. Future versions of SECTR_Audio may
/// further extend the feature set of music playback.
[RequireComponent(typeof(AudioListener))]
[RequireComponent(typeof(SECTR_Member))]
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Audio System")]
public class SECTR_AudioSystem : MonoBehaviour 
{
	#region Priate Details
	// Implementation of AudioCueInstance interface. Does much of
	// the heavy lifting necessary to actually play sounds.
	public class Instance : SECTR_IAudioInstance
	{
		#region External Interface Implementation
		public int Generation { get { return generation; } }

		public bool Active
		{
			get
			{
				#if UNITY_EDITOR
				if(!EditorApplication.isPaused)
				{
				#endif
					// Looping sounds are always active, as are playing and paused sounds,
					// except when fading out (i.e. stop has been called) at which point they
					// are logically complete.
					return (Loops || Delayed || (source && (source.isPlaying || Paused))) && !FadingOut;
					#if UNITY_EDITOR
				}
				else
				{
					//Since the editor is paused, we can't reliably check whether a clip is active, so we'll return true until the game is resumed.
					return true;
				}
				#endif
			}
		}

		public Vector3 Position
		{
			get
			{
				Vector3 worldSpacePosition = localPosition;
				if(parent)
				{
					if(ThreeD && Local)
					{
						worldSpacePosition += parent.transform.position;
					}
					else
					{
						worldSpacePosition = parent.localToWorldMatrix.MultiplyPoint3x4(worldSpacePosition);
					}
				}
				return worldSpacePosition;
			}
			set
			{
				if(parent)
				{
					if(ThreeD && Local)
					{
						localPosition = value - parent.transform.position;
					}
					else
					{
						localPosition = parent.worldToLocalMatrix.MultiplyPoint3x4(value);
					}
				}
				else
				{
					localPosition = value;
				}

				if(source)
				{
					source.transform.position = value;
				}
			}
		}

		public Vector3 LocalPosition
		{
			get { return localPosition; }
			set
			{
				this.localPosition = value;
				if(source)
				{
					source.transform.position = Position;
				}
			}
		}

        public Transform Parent { get { return parent; } }

        public float Volume
		{
			get { return userVolume; }
			set
			{
				if(userVolume != value)
				{
					userVolume = Mathf.Clamp01(value);
					Update(0f, true);
				}
			}
		}

		public float Pitch
		{
			get { return userPitch; }
			set
			{
				if(userPitch != value)
				{
					userPitch = Mathf.Clamp(value, -3f, 3f);
					Update(0f, true);
				}
			}
		}

		public bool Mute
		{
			get { return Muted; }
			set
			{
				if(Muted != value)
				{
					_SetFlag(Flags.Muted, value);
					if(source)
					{
						source.mute = value;
					}
				}
			}
		}

		public bool Pause
		{
			get { return Paused; }
			set
			{
				// Not using the Paused accessor as that also factors
				// in the global pause state.
				if(_GetFlag(Flags.Paused) != value)
				{
					_SetFlag(Flags.Paused, value);
					if(source)
					{
						if(value)
						{
							source.Pause();
						}
						else
						{
							source.Play();
						}
					}
				}
			}
		}

		public float TimeSeconds		
		{
			get { return source != null ? source.time : 0f; } 
			set
			{
				if(source)
				{
					source.time = value;
				}
			}
		}
		
		public int TimeSamples
		{
			get { return source != null ? source.timeSamples : 0; } 
			set
			{
				if(source)
				{
					source.timeSamples = value;
				}
			}
		}

		public void ForceInfinite()
		{
			_SetFlag(Flags.ForcedInfinite, true);
			_SetFlag(Flags.Local, true);
			_SetFlag(Flags.ThreeD, true);
			occlusionAlpha = 1;
			if(source)
			{
				source.rolloffMode = AudioRolloffMode.Linear;
				source.maxDistance = 1000000;
				source.minDistance = source.maxDistance - EPSILON;
				source.dopplerLevel = 0f;
			}
			Update(0f, true);
		}

		public void ForceOcclusion(bool occluded)
		{
			if(audioCue && audioCue.SourceCue.Spatialization == SECTR_AudioCue.Spatializations.Occludable3D)
			{
				_SetFlag(Flags.Occluded, occluded);
			}
		}

		public void SetParameter(string param, float value)
		{
#if RPC_SUPPORTED
			if(audioCue != null)
			{
				SECTR_AudioCue srcCue = audioCue.SourceCue;
				int numControlParams = srcCue.ControlParams.Count;
				volumeParamValues.Clear();
				pitchParamValues.Clear();
				for(int paramIndex = 0; paramIndex < numControlParams; ++paramIndex)
				{
					SECTR_CueParam cueParam = srcCue.ControlParams[paramIndex];
					if(cueParam.name == param)
					{
						paramTable[cueParam] = cueParam.curve.Evaluate(value);
					}

					float currentValue;
					if(paramTable.TryGetValue(cueParam, out currentValue))
					{
						switch(cueParam.affects)
						{
						case SECTR_CueParam.TargetType.Volume:
							volumeParamValues.Add(currentValue);
							break;
						case SECTR_CueParam.TargetType.Pitch:
							pitchParamValues.Add(currentValue);
							break;
						case SECTR_CueParam.TargetType.Attribute:
							attributeParamValues[cueParam.attributeData] = currentValue;
							break;
						}
					}
				}
			}
#endif
		}

		public AudioSource GetInternalAudioSource()
		{
			return source;
		}
		#endregion

		#region Internal Interface
		// Flags expressed as convenient functions for readability.
		public bool Loops 			{ get { return (flags & Flags.Loops) != 0; } }
		public bool Local 			{ get { return (flags & Flags.Local) != 0; } }
		public bool ThreeD 			{ get { return (flags & Flags.ThreeD) != 0; } }
		public bool FadingIn 		{ get { return (flags & Flags.FadingIn) != 0; } }
		public bool FadingOut 		{ get { return (flags & Flags.FadingOut) != 0; } }
		public bool Muted 			{ get { return (flags & Flags.Muted) != 0; } }
		public bool Paused 			{ get { return (flags & Flags.Paused) != 0 || AudioListener.pause; } }
		public bool HDR 			{ get { return (flags & Flags.HDR) != 0; } }
		public bool Occludable  	{ get { return (flags & Flags.Occludable) != 0; } }
		public bool Occluded		{ get { return (flags & Flags.Occluded) != 0; } }
		public bool ForcedInfinite	{ get { return (flags & Flags.ForcedInfinite) != 0; } }
		public bool Delayed			{ get { return (flags & Flags.Delayed) != 0; } }

		public SECTR_AudioBus Bus 	{ get { return audioCue != null ? audioCue.Bus : null; } }

		public SECTR_AudioCue Cue	{ get { return audioCue; } }

        public double KeepAliveTimeStamp = 0;

		// Init sets up this Instance. Should only ever be called once,
		// right after the Instance is returned from the pool.
		public void Init(SECTR_AudioCue audioCue, Transform parent, Vector3 localPosition, bool loops)
		{
			if(this.audioCue == null)
			{
				++generation;
				this.audioCue = audioCue;
				SECTR_AudioCue srcCue = audioCue.SourceCue;
				// Copy some properties into local flags for faster lookup.
				flags = 0;
				_SetFlag(Flags.Loops, loops);
				_SetFlag(Flags.Local, srcCue.IsLocal);
				_SetFlag(Flags.ThreeD, srcCue.Is3D);
				_SetFlag(Flags.HDR, srcCue.HDR);
				_SetFlag(Flags.Occludable, audioSystem.OcclusionFlags != 0 && srcCue.Spatialization == SECTR_AudioCue.Spatializations.Occludable3D);

				userVolume = 1f;
				userPitch = 1f;

				// Local sounds are always parented to the Listener, and ignore the passed in parent value.
				if(Local)
				{
					this.parent = Listener;
				}
				else
				{
					this.parent = parent;
				}

				this.localPosition = localPosition;

				int numParams = srcCue.ControlParams.Count;
				if(numParams > 0)
				{
					for(int paramIndex = 0; paramIndex < numParams; ++paramIndex)
					{
						SECTR_CueParam param = srcCue.ControlParams[paramIndex];
						SetParameter(param.name, param.defaultValue);
					}
				}

				_AddProximityInstance(srcCue);
				_ScheduleNextTest();
			}
		}

		// This is like Init and Play together, for very special uses only.
		public void Clone(Instance instance, Vector3 newPosition)
		{
			if(instance != null && instance.Active)
			{
				++generation;
				audioCue = instance.audioCue;
				flags = instance.flags;
				fadeStarTime = instance.fadeStarTime;
				basePitch = instance.basePitch;
				baseVolumeLoudness = instance.baseVolumeLoudness;
				userVolume = instance.userVolume;
				userPitch = instance.userPitch;
				occlusionAlpha = instance.occlusionAlpha;
				hdrCurve = instance.hdrCurve;
				parent = instance.parent;
				Position = newPosition;

				_AddProximityInstance(audioCue.SourceCue);
				_ScheduleNextTest();

				if(_AcquireSource())
				{
					Update(0f, true);
					if(source)
					{
						_SetFlag(Flags.Paused, false);
						source.clip = instance.source.clip;
						source.timeSamples = instance.source.timeSamples;
						source.Play();
					}
				}
			}
		}

		// The opposite of Init, Uninit should be called only once,
		// right before the Instance is returned to the pool.
		public void Uninit()
		{
            KeepAliveTimeStamp = 0;

			if(audioCue != null)
			{
				int proxLimit = audioCue.SourceCue.ProximityLimit;
				if(proxLimit > 0)
				{
					List<Instance> proxInstances;
					if(proximityTable.TryGetValue(audioCue, out proxInstances))
					{
						proxInstances.Remove(this);
					}
				}

				_ReleaseSource();

				// Do a bit of extra cleanup to make it ery clear that 
				// this Instance is no longer valid.
				audioCue = null;
				parent = null;
				flags = 0;
#if RPC_SUPPORTED
				if(paramTable.Count > 0)
				{
					paramTable.Clear();
					volumeParamValues.Clear();
					pitchParamValues.Clear();
					attributeParamBaseValues.Clear();
					attributeParamValues.Clear();
				}
#endif
			}
		}
	
		public void Play()
		{
			SECTR_AudioCue.ClipData nextClip = audioCue.GetNextClip();
			if(nextClip != null && nextClip.Clip != null && _AcquireSource())
			{
				if(nextClip.Clip.loadState == AudioDataLoadState.Unloaded)
				{
					nextClip.Clip.LoadAudioData();
				}
				SECTR_AudioCue srcCue = audioCue.SourceCue;
				if(srcCue.FadeInTime > 0)
				{
					fadeStarTime = currentTime;
					_SetFlag(Flags.FadingIn, true);
					_SetFlag(Flags.FadingOut, false);
				}
				if(Occludable && !ForcedInfinite)
				{
					_SetFlag(Flags.Occluded, IsOccluded(Position, audioSystem.OcclusionFlags));
					occlusionAlpha = Occluded ? 1f : 0f;
				}

				if(HDR)
				{
					baseVolumeLoudness = Random.Range(srcCue.Loudness.x, srcCue.Loudness.y);
				}
				else
				{
					baseVolumeLoudness = Random.Range(srcCue.Volume.x, srcCue.Volume.y);
				}
				baseVolumeLoudness *= nextClip.Volume;

				if(HDR)
				{
					if(nextClip.HDRCurve != null && nextClip.HDRCurve.length > 0)
					{
						hdrCurve = nextClip.HDRCurve;
					}
					else
					{
						Debug.LogWarning("Playing " + audioCue.name + " without HDR keys. Bake HDR keys for higher quality audio.");
					}
				}
				
				Update(0f, true);
				if(source)
				{
					_SetFlag(Flags.Paused, false);
					source.clip = nextClip.Clip;
					if(srcCue.Delay.y > 0f)
					{
						_SetFlag(Flags.Delayed, true);
						nextTestTime = currentTime + Random.Range(srcCue.Delay.x, srcCue.Delay.y);
					}
					else
					{
						source.Play();
					}
				}
			}
		}

		public void Stop(bool stopImmediately)
		{
			_SetFlag(Flags.Loops, false);
			_SetFlag(Flags.Delayed, false);
			_Stop(stopImmediately);
		}

		// The workhorse function of Instance, updates volume, position, etc.
		public void Update(float deltaTime, bool volumeOnly)
		{
			if(Delayed)
			{
				if(currentTime >= nextTestTime)
				{
					source.Play();
					_SetFlag(Flags.Delayed, false);
					_ScheduleNextTest();
				}
				else
				{
					return;
				}
			}

			SECTR_AudioCue srcCue = audioCue.SourceCue;
			Vector3 worldSpacePosition;
			if(ThreeD)
			{
				worldSpacePosition = Position;
				if(source)
				{
					source.transform.position = worldSpacePosition;
				}
			}
			else
			{
				worldSpacePosition = Listener.position;
			}

#if RPC_SUPPORTED
			int numParams = srcCue.ControlParams.Count;
#endif

			float fadeVolume = 1;
			if(FadingIn)
			{
				float fadeDelta = currentTime - fadeStarTime;
				fadeVolume = Mathf.Clamp01(fadeDelta / srcCue.FadeInTime);
				if(fadeVolume >= 1f)
				{
					_SetFlag(Flags.FadingIn, false);
				}
			}
			else if(FadingOut)
			{
				float fadeDelta = currentTime - fadeStarTime;
				fadeVolume = Mathf.Clamp01(1 - (fadeDelta / srcCue.FadeOutTime));
				if(fadeVolume <= 0f)
				{
					_SetFlag(Flags.FadingOut, false);
					_Stop(true);
				}
			}

			Vector3 listenerPos = Listener.transform.position;
			float listenerDistance = Vector3.Magnitude(worldSpacePosition - listenerPos);

			// Update the volume.
			if(source && (source.isPlaying || Paused || volumeOnly) && !Muted)
			{
				float busVolume;
				float busPitch;
#if UNITY_EDITOR
				if(audioCue == auditionCue)
				{
					busVolume = 1;
					busPitch = 1;
				}
				else
#endif
				{
					busVolume = audioCue.Bus ? audioCue.Bus.EffectiveVolume : audioSystem.MasterBus.EffectiveVolume;
					busPitch = audioCue.Bus ? audioCue.Bus.EffectivePitch : audioSystem.MasterBus.Pitch;
				}

				float paramVolume = 1f;
				float paramPitch = 1f;
#if RPC_SUPPORTED
				if(numParams > 0)
				{
					SetParameter("distance", listenerDistance);
					SetParameter("time", source.time);

					int numVolumeParams = volumeParamValues.Count;
					for(int paramIndex = 0; paramIndex < numVolumeParams; ++paramIndex)
					{
						paramVolume *= volumeParamValues[paramIndex];
					}

					int numPitchParams = pitchParamValues.Count;
					for(int paramIndex = 0; paramIndex < numPitchParams; ++paramIndex)
					{
						paramPitch *= pitchParamValues[paramIndex];
					}
				}
#endif

				// HDR sounds require custom processing. 
				float currentVolume = 1f;
				if(HDR)
				{
					float falloff = 1f;
					// Local sounds don't attenuate at all.
					if(!Local && listenerDistance > srcCue.MinDistance)					
					{
						// Non-local sounds will need their attenuation computed. We rely on
						// Unity/FMOD to spatialize them, but not to compute the attenuated volume,
						// as that volume is an input into the HDR mixer.
						float maxDistance = srcCue.MaxDistance;
						float minDistance = srcCue.MinDistance;

						switch(srcCue.Falloff)
						{
						case SECTR_AudioCue.FalloffTypes.Linear:
							falloff = 1f - Mathf.Clamp01((listenerDistance - minDistance) / (maxDistance - minDistance));
							break;
						case SECTR_AudioCue.FalloffTypes.Logarithmic:
							falloff = Mathf.Clamp01(1 / Mathf.Max(listenerDistance - minDistance - 1, EPSILON));
							if(listenerDistance > maxDistance)
							{
								falloff *= (1f - Mathf.Clamp01((listenerDistance - maxDistance) / AudioSystem.CullingBuffer));
							}
							break;
						}
					}

					float attenuatedLoudness = baseVolumeLoudness;
					if(hdrCurve != null)
					{
						float currentRMS = hdrCurve.Evaluate(source.time);
						attenuatedLoudness += currentRMS;
					}

					// Combine volume scalars, convert into dBs, and apply to attenuation.
					attenuatedLoudness += 20f * Mathf.Log10(Mathf.Max(userVolume * falloff, 0.001f));

					if(attenuatedLoudness < windowHDRMin)
					{
						// Sounds below the loudness floor are simply released. Looping sounds will be suspended,
						// while one-shots will be killed off.
						if(volumeOnly || ((baseVolumeLoudness - windowHDRMin) / audioSystem.HDRDecay > source.time - source.clip.length))
						{
							_Stop(false);
							return;
						}
					}

					// Scale by the fade and param volume after the culling check.
					attenuatedLoudness += 20f * Mathf.Log10(Mathf.Max(fadeVolume * paramVolume, 0.001f));

					// Apply our loudness to the current, total loudness,
					// but base our volume on the max loudness computed last frame.
					currentLoudness += Mathf.Pow(10f, attenuatedLoudness * .1f);
					currentVolume = Mathf.Clamp01(Mathf.Pow(10f, (attenuatedLoudness - windowHDRMax) * .05f));
				}
				else
				{
					currentVolume = baseVolumeLoudness * fadeVolume * userVolume * paramVolume;
				}

				// Apply occlussion, which for now is a simple scalar.
				if(Occludable)
				{
					float occludeRate = 1f;
					occlusionAlpha += deltaTime * (Occluded ? occludeRate : -occludeRate);
					occlusionAlpha = Mathf.Clamp01(occlusionAlpha);
					float effectiveOcclusion = occlusionAlpha * srcCue.OcclusionScale;
					currentVolume *= Mathf.Lerp(1f, audioSystem.OcclusionVolume, effectiveOcclusion);
					if(lowpass)
					{
						lowpass.enabled = occlusionAlpha > 0f;
						if(lowpass.enabled)
						{
							lowpass.cutoffFrequency = Mathf.Lerp(22000, audioSystem.OcclusionCutoff, effectiveOcclusion);
							lowpass.lowpassResonanceQ = Mathf.Lerp(1, audioSystem.OcclusionResonanceQ, effectiveOcclusion);
						}
					}
				}

				source.volume = Mathf.Clamp01(currentVolume * busVolume);
				source.pitch = Mathf.Clamp(userPitch * basePitch * paramPitch * busPitch, 0f, 2f);
			}

			// Sometimes we only want to update the volume (like on initial play),
			// if so, we're done now.
			if(volumeOnly)
			{
				return;
			}

			// Compute the near-2D blend for this Instance.
			if(source && (source.isPlaying || Paused) && !Local && audioSystem.BlendNearbySounds)
			{
				float spatialBlend = 0f;
				if(listenerDistance <= audioSystem.NearBlendRange.x)
				{
					spatialBlend = 0f;
				}
				else if(listenerDistance <= audioSystem.NearBlendRange.y)
			   	{
					spatialBlend = Mathf.Clamp01(listenerDistance - audioSystem.NearBlendRange.x) / (audioSystem.NearBlendRange.y - audioSystem.NearBlendRange.x);
				}
				else
				{
					spatialBlend = 1f;
				}

				source.spatialBlend = spatialBlend;
			}

			// For looping sounds, we need to periodically activate/suspend them.
			if(Loops && !Paused)
			{
				bool actuallyPlaying = (source != null) && (source.isPlaying);
				bool canPlayHDR = !actuallyPlaying && (!HDR || baseVolumeLoudness >= windowHDRMin);
				if(Local)
				{
					if(!actuallyPlaying && canPlayHDR && _CheckInstances(audioCue, actuallyPlaying))
					{
						Play();
					}
				}
				else
				{
					// Time checks are done in real time to better match the fact that audio is
					// always running. Also, so that this works in the editor.
					if(currentTime >= nextTestTime)
					{
						bool inRange = _CheckProximity(audioCue, parent, localPosition, this);
						if(inRange && !actuallyPlaying && canPlayHDR && _CheckInstances(audioCue, actuallyPlaying))
						{
							Play();
						}
						else if(!inRange && actuallyPlaying)
						{
							_Stop(true);
						}
						else if(Occludable && !ForcedInfinite)
						{
							_SetFlag(Flags.Occluded, IsOccluded(worldSpacePosition, audioSystem.OcclusionFlags));
						}
						_ScheduleNextTest();
					}
				}
			}

#if RPC_SUPPORTED
			if(source && numParams > 0)
			{
				Dictionary<SECTR_CueParam.AttributeData, float>.Enumerator iter;

				iter = attributeParamBaseValues.GetEnumerator();
				while(iter.MoveNext())
				{
					SECTR_CueParam.AttributeData paramData = iter.Current.Key;
					Component component = paramData.ComponentType != null ? source.GetComponent(paramData.ComponentType) : null;
					if(component)
					{
						if(paramData.fieldAttribute)
						{
							FieldInfo field = paramData.ComponentType.GetField(paramData.attributeName);
							if(field != null)
							{
								field.SetValue(component, iter.Current.Value);
							}
						}
						else
						{
							PropertyInfo property = paramData.ComponentType.GetProperty(paramData.attributeName);
							if(property != null)
							{
								property.SetValue(component, iter.Current.Value, null);
							}
						}
					}
				}

				iter = attributeParamValues.GetEnumerator();
				while(iter.MoveNext())
				{
					SECTR_CueParam.AttributeData paramData = iter.Current.Key;
					Component component = paramData.ComponentType != null ? source.GetComponent(paramData.ComponentType) : null;
					if(component)
					{
						if(paramData.fieldAttribute)
						{
							FieldInfo field = paramData.ComponentType.GetField(paramData.attributeName);
							if(field != null)
							{
								float currentValue = (float)field.GetValue(component);
								field.SetValue(component, currentValue * iter.Current.Value);
							}
						}
						else
						{
							PropertyInfo property = paramData.ComponentType.GetProperty(paramData.attributeName);
							if(property != null)
							{
								float currentValue = (float)property.GetValue(component, null);
								property.SetValue(component, currentValue * iter.Current.Value, null);
							}
						}
					}
				}
#endif
			}
		}

#if UNITY_EDITOR
		public void Reset()
		{
			int time = TimeSamples;
			_Stop(true);
			SECTR_AudioCue origCue = audioCue;
			audioCue = null;
			Init(origCue, parent, localPosition, Loops);
			Play();
			TimeSamples = time;
		}
#endif
		#endregion

		#region Private Details
		private int generation = 0;
		private AudioSource source = null;
		private AudioLowPassFilter lowpass = null;
		private SECTR_AudioCue audioCue;
		private Transform parent = null;
		private Vector3 localPosition = Vector3.zero;
		private Flags flags = 0;
		private float nextTestTime = 0;
		private float fadeStarTime = 0;
		private float basePitch = 1;
		private float baseVolumeLoudness = 1;
		private float userVolume = 1;
		private float userPitch = 1;
		private float occlusionAlpha = 1;
		private AnimationCurve hdrCurve = null;
#if RPC_SUPPORTED
		private Dictionary<SECTR_CueParam, float> paramTable = new Dictionary<SECTR_CueParam, float>();
		private List<float> volumeParamValues = new List<float>();
		private List<float> pitchParamValues = new List<float>();
		private Dictionary<SECTR_CueParam.AttributeData, float> attributeParamValues = new Dictionary<SECTR_CueParam.AttributeData, float>();
		private Dictionary<SECTR_CueParam.AttributeData, float> attributeParamBaseValues = new Dictionary<SECTR_CueParam.AttributeData, float>();
#endif

		[System.Flags]
		private enum Flags
		{
			Loops			= 1 << 0,
			FadingIn		= 1 << 1,
			FadingOut		= 1 << 2,
			Muted			= 1 << 3,
			Local			= 1 << 4,
			ThreeD			= 1 << 5,
			Paused			= 1 << 6,
			HDR				= 1 << 7,
			Occludable  	= 1 << 8,
			Occluded  		= 1 << 9,
			ForcedInfinite  = 1 << 10,
			Delayed			= 1 << 11,
		};
		
		private void _SetFlag(Flags flag, bool on)
		{
			if(on)
			{
				flags |= flag;
			}
			else
			{
				flags &= ~flag;
			}
		}

		private bool _GetFlag(Flags flag)
		{
			return ((flags & flag) != 0);
		}

		// Grabs an actual AudioSource from the pool, and configures is.
		private bool _AcquireSource()
		{
			if(!source)
			{
				SECTR_AudioCue srcCue = audioCue.SourceCue;

				bool useLowpass = Occludable && !srcCue.BypassEffects && SECTR_Modules.HasPro();
				if(srcCue.Prefab != null)
				{
					Stack<AudioSource> prefabPool = null;
					if(!prefabSourcePool.TryGetValue(srcCue.Prefab, out prefabPool))
					{
						prefabPool = new Stack<AudioSource>(8);
						prefabSourcePool[srcCue.Prefab] = prefabPool;
					}
					if(prefabPool.Count > 0)
					{
						source = prefabPool.Pop();
						if(useLowpass)
						{
							lowpass = source.GetComponent<AudioLowPassFilter>();
							if(lowpass == null)
							{
								lowpass = source.gameObject.AddComponent<AudioLowPassFilter>();
							}
						}
					}
					else
					{
						GameObject sourceObject = (GameObject)Instantiate(srcCue.Prefab);
						source = sourceObject.GetComponent<AudioSource>();
						if(source == null)
						{
							source = sourceObject.AddComponent<AudioSource>();
						}
						if(useLowpass)
						{
							lowpass = source.GetComponent<AudioLowPassFilter>();
							if(lowpass == null)
							{
								lowpass = source.gameObject.AddComponent<AudioLowPassFilter>();
							}
						}
						sourceObject.hideFlags = HideFlags.HideAndDontSave;
						sourceObject.transform.parent = sourcePoolParent;
					}
				}
				else
				{
					useLowpass = useLowpass && lowpassSourcePool.Count > 0;
					source = useLowpass ? lowpassSourcePool.Pop() : simpleSourcePool.Pop();
				}

				if(source) // The pool may be empty.
				{
					if(useLowpass)
					{
						lowpass = source.GetComponent<AudioLowPassFilter>();
						lowpass.enabled = false;
					}

					source.volume = 1f;
					source.pitch = 1f;
					source.time = 0;
					source.timeSamples = 0;
					source.priority = srcCue.Priority;
					source.bypassEffects = srcCue.BypassEffects;
					source.bypassReverbZones = srcCue.BypassEffects;
					// Local sounds will be looped by SECTR Audio, so that things like shuffle
					// "just work". Non-local sounds will by looped by Unity, to avoid having to
					// do an active and a distance check every frame.
					source.loop = srcCue.Loops;
					source.spread = srcCue.Spread;
					source.mute = Muted;
					basePitch = Random.Range(srcCue.Pitch.x, srcCue.Pitch.y);
					
					if(srcCue.MaxInstances > 0)
					{
						int curInstances;
						if(maxInstancesTable.TryGetValue(audioCue, out curInstances))
						{
							maxInstancesTable[audioCue] = ++curInstances;
						}
						else
						{
							maxInstancesTable.Add(audioCue, 1);
						}
					}

					// Set to defauls so that they aren't inherited from previous instances.
					source.panStereo = 0f;
					source.spatialBlend = 1f;

					if(Local)
					{
						if(ThreeD)
						{
							source.rolloffMode = AudioRolloffMode.Linear;
							source.maxDistance = 1000000;
							source.minDistance = source.maxDistance - EPSILON;
						}
						else
						{
							source.panStereo = srcCue.Pan2D;
							source.spatialBlend = 0;
						}
																	
						source.dopplerLevel = 0f;

						// Auto-set a very high priority for music and background ambiences to ensure
						// that they don't cut out when lots of sounds play.
						if((currentAmbience != null && currentAmbience.BackgroundLoop == audioCue) ||
						   (currentMusic != null && currentMusic == audioCue))
						{
							source.priority = 0;
						}
					}
					else
					{
						if(HDR)
						{
							// HDR sounds will be spatialzed by Unity, but not attenuated. To prevent them
							// from attenuating, simply set a very, very large min and max distance.
							source.rolloffMode = AudioRolloffMode.Linear;
							// Min must be set before Max or Unity may ignore it.
							source.minDistance = 1000000;
							source.maxDistance = source.minDistance + EPSILON;
						}
						else
						{
							// LDR sounds can have Unity do the actual attenuation, so just pass it along.
							switch(srcCue.Falloff)
							{
							case SECTR_AudioCue.FalloffTypes.Logarithmic:
								source.rolloffMode = AudioRolloffMode.Logarithmic;
								break;
							case SECTR_AudioCue.FalloffTypes.Linear:
							default:
								source.rolloffMode = AudioRolloffMode.Linear;
								break;
							}
							// Min must be set before Max or Unity may ignore it.
							source.minDistance = srcCue.MinDistance;
							source.maxDistance = Mathf.Max(srcCue.MaxDistance, srcCue.MinDistance + EPSILON);
						}
						source.dopplerLevel = srcCue.DopplerLevel;
						source.velocityUpdateMode = AudioVelocityUpdateMode.Dynamic;
					}
					source.transform.position = Position;
					source.gameObject.SetActive(true);

#if RPC_SUPPORTED
					int numParams = srcCue.ControlParams.Count;
					for(int paramIndex = 0; paramIndex < numParams; ++paramIndex)
					{
						SECTR_CueParam param = srcCue.ControlParams[paramIndex];
						if(param.affects == SECTR_CueParam.TargetType.Attribute)
						{
							SECTR_CueParam.AttributeData paramData = param.attributeData;
							Component component = paramData.ComponentType != null ? source.GetComponent(paramData.ComponentType) : null;
							if(component)
							{
								if(param.attributeData.fieldAttribute)
								{
									FieldInfo field = paramData.ComponentType.GetField(param.attributeData.attributeName);
									if(field != null)
									{
										attributeParamBaseValues[param.attributeData] = (float)field.GetValue(component);
									}
								}
								else
								{
									PropertyInfo property = paramData.ComponentType.GetProperty(param.attributeData.attributeName);
									if(property != null)
									{
										attributeParamBaseValues[param.attributeData] = (float)property.GetValue(component, null);
									}
								}
							}
						}
					}
#endif
				}
			}

			return source != null;
		}

		// The opposite of _AcquireSource, returns a source to the pool.
		private void _ReleaseSource()
		{
			if(source != null)
			{
				SECTR_AudioCue srcCue = audioCue.SourceCue;

				if(srcCue.MaxInstances > 0)
				{
					int curInstances;
					if(maxInstancesTable.TryGetValue(audioCue, out curInstances))
					{
						--curInstances;
						if(curInstances <= 0)
						{
							maxInstancesTable.Remove(audioCue);
						}
						else
						{
							maxInstancesTable[audioCue] = curInstances;
						}
					}
				}
				
				source.Stop();
				source.clip = null;
				source.gameObject.SetActive(false);

				if(srcCue.Prefab)
				{
#if UNITY_EDITOR
					if(Application.isEditor && !Application.isPlaying)
					{
						GameObject.DestroyImmediate(source.gameObject);
					}
					else
#endif						
					{
						prefabSourcePool[srcCue.Prefab].Push(source);
					}
				}
				else if(lowpass)
				{
					lowpass.enabled = false;
					lowpassSourcePool.Push(source);
				}
				else
				{
					simpleSourcePool.Push(source);
				}
				source = null;
				lowpass = null;
				hdrCurve = null;
			}
		}

		private void _AddProximityInstance(SECTR_AudioCue srcCue)
		{
			int proxLimit = srcCue.ProximityLimit;
			if(proxLimit > 0)
			{
				List<Instance> proxInstances;
				if(!proximityTable.TryGetValue(audioCue, out proxInstances))
				{
					proxInstances = new List<Instance>(proxLimit * 2);
					proximityTable[audioCue] = proxInstances;
				}
				proxInstances.Add(this);
			}
		}
		
		private void _ScheduleNextTest()
		{
			if (nextTestTime < currentTime)
			{
				nextTestTime = currentTime + Random.Range(audioSystem.RetestInterval.x, audioSystem.RetestInterval.y);
			}
		}

		private void _Stop(bool stopImmediately)
		{
			if(!stopImmediately && source && source.isPlaying && audioCue && audioCue.SourceCue.FadeOutTime > 0)
			{
				if(FadingIn)
				{
					// It's possible that we're stopped while fading in, so to keep the volume from
					// popping we simply adjust the fadeStartTime of the fade out.
					float curFade = 1 - Mathf.Clamp01((currentTime - fadeStarTime) / audioCue.SourceCue.FadeInTime);
					fadeStarTime = currentTime - (curFade * audioCue.SourceCue.FadeOutTime);
				}
				else
				{
					fadeStarTime = currentTime;
				}
				_SetFlag(Flags.FadingOut, true);
				_SetFlag(Flags.FadingIn, false);
			}
			else
			{
                if (KeepAliveTimeStamp == 0)
                {
                    KeepAliveTimeStamp = GetUnixTimeStamp();
                }

                if (GetUnixTimeStamp() >= KeepAliveTimeStamp + audioCue.KeepAliveTime)
                {
                    _SetFlag(Flags.FadingOut, false);
                    _ReleaseSource();
                }
                else
                {
                    _SetFlag(Flags.FadingOut, true);
                }
			}
		}
		#endregion
	}

	// Singleton
	private static SECTR_AudioSystem audioSystem = null;

	// Instances
	private static Stack<Instance> instancePool = null;
	private static Stack<AudioSource> simpleSourcePool = null;
	private static Stack<AudioSource> lowpassSourcePool = null;
	private static Dictionary<GameObject, Stack<AudioSource>> prefabSourcePool = null;
	private static Transform sourcePoolParent = null;
	private static List<Instance> activeInstances = null;
	private static Dictionary<SECTR_AudioCue, int> maxInstancesTable = null;
	private static Dictionary<SECTR_AudioCue, List<Instance>> proximityTable = null;

	// Time
	private static float currentTime = 0;

	// Ambience
	private static List<SECTR_AudioAmbience> ambienceStack;
	private static SECTR_AudioAmbience currentAmbience = null;
	private static SECTR_AudioCueInstance ambienceLoop;
	private static SECTR_AudioCueInstance ambienceOneShot;
	private static float nextAmbienceOneShotTime = 0;

	// Music
	private static SECTR_AudioCue currentMusic = null;
	private static SECTR_AudioCueInstance musicLoop;

	// HDR
	private static float windowHDRMax = 0;
	private static float windowHDRMin = 0;
	private static float currentLoudness = 0;

	// Occlusion
	private static List<SECTR_Graph.Node> occlusionPath;

	private static SECTR_Member cachedMember = null;

	private const float EPSILON = 0.001f;

	#if AUDIO_HUD
	private Dictionary<SECTR_AudioCue, SECTR_AudioCue> hudInstances = new Dictionary<SECTR_AudioCue, SECTR_AudioCue>(32);
	private const int maxHDRVolumes = 512;
	private int hdrVolumeIndex = 0;
	private float[] hdrVolumes = new float[maxHDRVolumes];
	private LineRenderer lineRenderer;
	private bool activeHDRSounds;
	#endif

	#if UNITY_EDITOR
	private static SECTR_AudioCue auditionCue = null;
	private static SECTR_AudioCueInstance auditionInstance;
	private static int auditionCueID = -1;
	private static List<SECTR_AudioBus> soloBuses = new List<SECTR_AudioBus>();
	private static OcclusionModes oldOcclusionFlags = 0;
	#endif
	#endregion

	#region Public Interface
	/// Flag set that determines which rules to use when computing audio Occlusion.
	[System.Flags]
	public enum OcclusionModes
	{
		/// Uses the Sector/Portal graph to compute occlusion. Sound is occluded if it passes through a Closed Portal.
		Graph 			= 1 << 0,		
		/// Uses the raycasts to compute occlusion. Sound is occluded if it passes through a collider. 
		Raycast			= 1 << 1,		
		/// Uses the distance to compute occlusion. Sound is occluded if it is more than a certain distance from the listener.
		Distance		= 1 << 2, 		
	}

	// Basic Properties
	[SECTR_ToolTip("The maximum number of instances that can be active at once. Inaudible sounds do not count against this limit.")]
	public int MaxInstances = 128;
	[SECTR_ToolTip("The number of instances to allocate with lowpass effects (for occlusion and the like).")]
	public int LowpassInstances = 32;
	[SECTR_ToolTip("The Bus at the top of the mixing heirarchy. Required to play sounds.", null, false)]
	public SECTR_AudioBus MasterBus = null;
	[SECTR_ToolTip("The baseline settings for any environmental audio. Will be audible when no other ambiences are active.")]
	public SECTR_AudioAmbience DefaultAmbience = new SECTR_AudioAmbience();
	// HDR
	[SECTR_ToolTip("Minimum Loudness for the HDR mixer. Current Loudness will never drop below this.", 0f, 200f)]
	public float HDRBaseLoudness = 50;
	[SECTR_ToolTip("The maximum difference between the loudest sound and the softest sound before sounds are simply culled out.", 0f, 200f)]
	public float HDRWindowSize = 50;
	[SECTR_ToolTip("Speed at which HDR window decays after a loud sound is played.", 0f, 100f)]
	public float HDRDecay = 1;
	// Near Blend
	[SECTR_ToolTip("Should sounds close to the listener be blended into 2D (to avoid harsh stereo switching).")]
	public bool BlendNearbySounds = true;
	[SECTR_ToolTip("Objects close to the listener will be blended into 2D, as a kind of fake HRTF. This determines the start and end of that blend.", "BlendNearbySounds")]
	public Vector2 NearBlendRange = new Vector2(0.25f, 0.75f);
	// Occlusion
	[SECTR_ToolTip("Determines what kind of logic to use for computing sound occlusion.", null, typeof(OcclusionModes))]
	public OcclusionModes OcclusionFlags = 0;
	[SECTR_ToolTip("The distance beyond which sounds will be considered occluded, if Distance occlusion is enabled.", "OcclusionFlags")]
	public float OcclusionDistance = 100f;
	[SECTR_ToolTip("The layers to test against when raycasting for occlusion.", "OcclusionFlags")]
	public LayerMask RaycastLayers = Physics.DefaultRaycastLayers;

	[SECTR_ToolTip("The amount by which to decrease the volume of occluded sounds.", "OcclusionFlags", 0f, 1f)]
    public float OcclusionVolume = 0.5f;
	[SECTR_ToolTip("The frequency cutoff of the lowpass filter for occluded sounds.", "OcclusionFlags", 10f, 22000f)]
	public float OcclusionCutoff = 2200;
	[SECTR_ToolTip("The resonance Q of the lowpass filter for occluded sounds.", "OcclusionFlags", 1f, 10f)]
	public float OcclusionResonanceQ = 1;

	// Advanced
	[SECTR_ToolTip("The amount of time between tests to see if looping sounds should start or stop running.")]
	public Vector2 RetestInterval = new Vector2(0.5f, 1f);
	[SECTR_ToolTip("The amount of buffer to give before culling distant sounds.")]
	public float CullingBuffer = 10f;

	// Debugging
	[SECTR_ToolTip("Enable or disable of the in-game audio HUD.", true)]
	public bool ShowAudioHUD = false;
	[SECTR_ToolTip("Material to use to render HUD lines.", true)]
	public Material HUDLineMaterial = null;
	[SECTR_ToolTip("In the editor only, puts the listener at the AudioSystem, not at the Scene Camera.", true)]
	public bool Debugging = false;

	/// Returns true if there is an active AudioSystem in the scene.
	public static bool Initialized 			{ get { return audioSystem != null; } }

	/// Quick accessor for the Member of the Audio System.
	public static SECTR_Member Member		{ get { return cachedMember; } }

	/// Quick accessor for the active AudioSystem.
	public static SECTR_AudioSystem AudioSystem	{ get { return audioSystem; } }

	/// Accessor for the Listener, which has different behavior in game and in the editor.
	public static Transform Listener
	{
		get
		{
#if UNITY_EDITOR
			if(!audioSystem.Debugging && Application.isEditor && !Application.isPlaying &&
#if UNITY_2019_1_OR_NEWER
                SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.audioPlay && SceneView.lastActiveSceneView.camera)
#else
                 SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.m_AudioPlay && SceneView.lastActiveSceneView.camera)
#endif
            {
                return SceneView.lastActiveSceneView.camera.transform;
			}
#endif
			return audioSystem.transform;
		}
	}

    public static List<Instance> ActiveInstances { get { return activeInstances; } }

    /// Play an AudioCue at the specified position.
    /// <param name="audioCue">The SECTR_AudioCue to play.</param>
    /// <param name="position">The world space position at which to play the Cue.</param>
    /// <param name="loop">Forces this Cue to loop, even if it would not otherwise.</param>
    /// <returns>A handle to the created instance.</returns>
    public static SECTR_AudioCueInstance Play(SECTR_AudioCue audioCue, Vector3 position, bool loop)
	{
		return SECTR_AudioSystem.Play(audioCue, null, position, loop);
	}
	
	/// Play an AudioCue at the specified position relative to the parent transform (if there is on).
	/// <param name="audioCue">The SECTR_AudioCue to play.</param>
	/// <param name="parent">An optional parent transform. If specified, position will be local to that transform.</param>
	/// <param name="localPosition">The world space position at which to play the Cue.</param>
	/// <param name="loop">Forces this Cue to loop, even if it would not otherwise.</param>
	/// <returns>A handle to the created instance.</returns>
	public static SECTR_AudioCueInstance Play(SECTR_AudioCue audioCue, Transform parent, Vector3 localPosition, bool loop)
	{
		// Lots of things can cause audio not to play, so check from them here,
		// and warng if appropriate.
		if(!Initialized)
		{
			Debug.LogWarning("Cannot play sounds before SECTR_AudioSystem is initialized.");
			return new SECTR_AudioCueInstance();
		}
		else if(audioSystem.MasterBus == null)
		{
			Debug.LogWarning("SECTR_AudioSystem needs a Master Bus before you can play sounds.");
			return new SECTR_AudioCueInstance();
		}
		else if(activeInstances.Count >= audioSystem.MaxInstances || instancePool.Count == 0)
		{
			Debug.LogWarning("Global max audio instances exceeded.");
			return new SECTR_AudioCueInstance();
		}
		else if(audioCue == null || !_CheckInstances(audioCue, false))
		{
			// It's not necessarily bad to pass in a null cue or to not have
			// enough instances, so no warning here.
			return new SECTR_AudioCueInstance();
		}
		else if(audioCue.AudioClips.Count == 0)
		{
			Debug.LogWarning("Cannot play a clipless Audio Cues.");
			return new SECTR_AudioCueInstance();
		}
	
		SECTR_AudioCue srcCue = audioCue.SourceCue;
		if(Random.value <= srcCue.PlayProbability)
		{
			bool inRange = srcCue.IsLocal || _CheckProximity(audioCue, parent, localPosition, null);
			loop |= srcCue.Loops;

			// We'll play any sound that loops or is in range. Out of range one shots
			// will be pre-culled and never play, becase they will probably be done
			// by the time they are audible.
			if(inRange || loop)
			{
				Instance newInstance = instancePool.Pop();
				activeInstances.Add(newInstance);
				newInstance.Init(audioCue, parent, localPosition, loop);
				if(inRange)
				{
					newInstance.Play();
				}
				return new SECTR_AudioCueInstance(newInstance, newInstance.Generation);
			}
		}

		return new SECTR_AudioCueInstance();
	}

	/// Play an AudioCue at the specified position.
	/// <param name="instance">The SECTR_AudioCueInstance to duplicate.</param>
	/// <param name="newPosition">The world space position for the new instance.</param>
	/// <returns>A handle to the created instance.</returns>
	public static SECTR_AudioCueInstance Clone(SECTR_AudioCueInstance instance, Vector3 newPosition)
	{
		if(instance && instancePool.Count > 0)
		{
			Instance newInstance = instancePool.Pop();
			activeInstances.Add(newInstance);
			newInstance.Clone((Instance)instance.GetInternalInstance(), newPosition);
			return new SECTR_AudioCueInstance(newInstance, newInstance.Generation);
		}

		return new SECTR_AudioCueInstance();
	}

	/// Playes the specified Cue as music. Will soft-stop any
	/// currently playing music. Music should be 2D.
	/// <param name="musicCue">The Cue to play.</param>
	public static void PlayMusic(SECTR_AudioCue musicCue)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot play music before Audio System is initialized.");
		}
		else if(musicCue != null)
		{
			if(musicCue.Is3D)
			{
				Debug.LogWarning("Music Cue " + musicCue.name + "is 3D but music should be Simple 2D.");
			}
			
			musicLoop.Stop(false);			

			currentMusic = musicCue;
			musicLoop = Play(currentMusic, Listener, Vector3.zero, true);
		}
	}
	
	/// Stops the currently playing music.
	/// <param name="stopImmediate">If set to <c>true</c> stop immediate.</param>
	public static void StopMusic(bool stopImmediate)
	{
		if(Initialized)
		{
			musicLoop.Stop(stopImmediate);
			currentMusic = null;
		}
	}
	
	/// Pushes the specified environment onto the stack of active envrionemnts.
	/// The item at the top of the stack will be audible.
	/// <param name="ambience">The ambience to add.</param>
	public static void PushAmbience(SECTR_AudioAmbience ambience)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot activate an ambience before audio system is initialzied.");
		}
		else if(ambience != null)
		{
			ambienceStack.Add(ambience);
		}
	}
	
	/// Removes the specified environment from the stack of active envrionemnts.
	/// If it was at the top, the next highest will become active.
	/// <param name="ambience">The ambience to remove.</param>
	public static void RemoveAmbience(SECTR_AudioAmbience ambience)
	{
		if(Initialized && ambience != null)
		{
			ambienceStack.Remove(ambience);
		}
	}

	/// Sets user volume of the specified bus, if it exists.
	/// This is applied on top of whatever volume is set in the Bus resource.
	/// <param name="busName">The name of the bus to mute.</param>
	/// <param name="volume">The volume level.</param>
	public static void SetBusVolume(string busName, float volume)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot activate an ambience before audio system is initialzied.");
		}
		else if(!string.IsNullOrEmpty(busName))
		{
			SetBusVolume(_FindBus(audioSystem.MasterBus, busName), volume);
		}
	}
	
	/// Sets user volume of the specified bus, if it exists.
	/// This is applied on top of whatever volume is set in the Bus resource.
	/// <param name="bus">The bus object to mute.</param>
	/// <param name="volume">The volume level.</param>
	public static void SetBusVolume(SECTR_AudioBus bus, float volume)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot set bus volume before Audio System is initialzied.");			
		}
		else if(bus)
		{
			bus.UserVolume = volume;
		}
	}
	
	/// Sets the mute state of the specified bus, if it exists.
	/// <param name="busName">The name of the bus to mute.</param>
	/// <param name="mute">Mute on or off.</param>
	public static void MuteBus(string busName, bool mute)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot mute bus before Audio System is initialzied.");			
		}
		else if(!string.IsNullOrEmpty(busName))
		{
			MuteBus(_FindBus(audioSystem.MasterBus, busName), mute);
		}
	}

	/// Sets the mute state of the specified bus.
	/// <param name="bus">The bus object to mute.</param>
	/// <param name="mute">Mute on or off.</param>
	public static void MuteBus(SECTR_AudioBus bus, bool mute)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot mute bus before Audio System is initialzied.");			
		}
		else if(bus)
		{
			bus.Muted = mute;
		}
	}

	/// (Un)pauses the specified bus.
	/// <param name="bus">The bus object to pause.</param>
	/// <param name="paused">Pause or unpause.</param>
	public static void PauseBus(SECTR_AudioBus bus, bool paused)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot pause bus before Audio System is initialzied.");			
		}
		else if(bus)
		{
			int numInstances = activeInstances.Count;
			for(int instanceIndex = 0; instanceIndex < numInstances; ++instanceIndex)
			{
				Instance instance = activeInstances[instanceIndex];
				if(bus.IsAncestorOf(instance.Bus))
				{
					instance.Pause = paused;
				}
			}
		}
	}

	// Computes the occlusion for this sound, using the graph or a raycast or both.
	public static bool IsOccluded(Vector3 worldSpacePosition, OcclusionModes occlusionFlags)
	{
		bool occluded = false;
		Vector3 listenerPosition = Listener.position;
		Vector3 toListener = listenerPosition - worldSpacePosition;
		float distancetoListenerSqr = toListener.sqrMagnitude;
		// Distant occlusion
		if(!occluded && ((occlusionFlags & OcclusionModes.Distance) != 0))
		{
			occluded = distancetoListenerSqr >= (audioSystem.OcclusionDistance * audioSystem.OcclusionDistance); 
		}
		// Raycast occlusion
		if(!occluded && ((occlusionFlags & OcclusionModes.Raycast) != 0))
		{
			float distancetoListener = Mathf.Sqrt(distancetoListenerSqr);
			RaycastHit hit;
			bool hitSomething = Physics.Raycast(worldSpacePosition, toListener, out hit, distancetoListener, audioSystem.RaycastLayers); 
			occluded = hitSomething && hit.transform != Listener;
		}
		// Graph occlusion
		if(!occluded && ((occlusionFlags & OcclusionModes.Graph) != 0))
		{
			// Note that we can't use Closed as a stop flag because there may be another longer,
			// but still valid path from source to listener.
			SECTR_Graph.FindShortestPath(ref occlusionPath, worldSpacePosition, listenerPosition, 0);
			int numNodes = occlusionPath.Count;
			for(int nodeIndex = 0; nodeIndex < numNodes && !occluded; ++nodeIndex)
			{
				SECTR_Graph.Node node = occlusionPath[nodeIndex];
				if(node.Portal && ((node.Portal.Flags & SECTR_Portal.PortalFlags.Closed) != 0))
				{
					occluded = true;
				}
			}
		}
		return occluded;
	}

#if UNITY_EDITOR
	public static void Audition(SECTR_AudioCue audioCue)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot audition before Audio System is initialzied.");			
		}
		else if(audioCue)
		{
			StopAudition();
			if(audioCue.GetInstanceID() != auditionCueID ||
			   audioCue.AudioClips.Count != auditionCue.AudioClips.Count)
			{
				auditionCue.ClearClips();
				auditionCue.AudioClips = new List<SECTR_AudioCue.ClipData>(audioCue.AudioClips);
			}
			auditionCue.PlaybackMode = audioCue.PlaybackMode;
			auditionCue.FadeInTime = audioCue.FadeInTime;
			auditionCue.FadeOutTime = audioCue.FadeOutTime;
			auditionCue.Volume = audioCue.Volume;
			auditionCue.Loudness = audioCue.Loudness;
			auditionCue.Pitch = audioCue.Pitch;
			auditionCue.Prefab = audioCue.Prefab;
			auditionCueID = audioCue.GetInstanceID();
			auditionInstance = Play(auditionCue, Listener.transform, Vector3.zero, false);
		}
	}

	public static void Audition(AudioClip clip)
	{
		if(!Initialized)
		{
			Debug.LogWarning("Cannot audition before Audio System is initialzied.");			
		}
		else
		{
			StopAudition();
			auditionCue.ClearClips();
			auditionCue.AddClip(clip, true);
			auditionCue.PlaybackMode = SECTR_AudioCue.PlaybackModes.Random;
			auditionCue.FadeInTime = 0f;
			auditionCue.FadeOutTime = 0f;
			auditionCue.Volume = new Vector2(1f, 1f);
			auditionCue.Loudness = new Vector2(50f, 50f);
			auditionCue.Pitch = new Vector2(1f, 1f);
			auditionCueID = -1;
			auditionInstance = Play(auditionCue, Listener.transform, Vector3.zero, false);
		}
	}
	
	public static void StopAudition()
	{
		if(Initialized)
		{
			auditionInstance.Stop(false);
		}
	}
	
	public static bool IsAuditioning()
	{
		return Initialized && auditionInstance;
	}

	public static void Solo(SECTR_AudioBus bus, bool solo)
	{
		if(soloBuses != null)
		{
			if(solo && !BusIsSolo(bus))
			{
				soloBuses.Add(bus);
			}
			else if(!solo)
			{
				soloBuses.Remove(bus);
			}
		}
	}

	public static bool BusIsSolo(SECTR_AudioBus bus)
	{
		return soloBuses != null && soloBuses.Contains(bus);
	}

	public static void StopSoloing()
	{
		if(soloBuses != null)
		{
			soloBuses.Clear();
		}
	}
#endif
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		// AudioSystem is a singleton, so there can only ever be one.
		if(audioSystem && audioSystem != this)
		{
#if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
#if UNITY_2018_3_OR_NEWER
                //Additional check for new Prefab Stage introduced in 2018.3
                if(PrefabStageUtility.GetCurrentPrefabStage()==null)
                {
#endif
                GameObject.DestroyImmediate(this);
                Debug.LogWarning("More than one Audio System is not allowed, removing extra Audio System from Game Object '" + gameObject.name + "'");
#if UNITY_2018_3_OR_NEWER
                }
#endif
			}
			else
#endif
                {
                    GameObject.Destroy(this);
			}
		}
		else if(audioSystem == null)
		{
			audioSystem = this;

			// Allocate all of of data structures, and pools.
			instancePool = new Stack<Instance>(MaxInstances);
			for(int instanceIndex = 0; instanceIndex < MaxInstances; ++instanceIndex)
			{
				instancePool.Push(new Instance());
			}

			int numSimpleSources = SECTR_Modules.HasPro() ? Mathf.Max(0, MaxInstances - LowpassInstances) : MaxInstances;
			int numLowpassInstances = MaxInstances - numSimpleSources;
			simpleSourcePool = new Stack<AudioSource>(numSimpleSources);
			lowpassSourcePool = SECTR_Modules.HasPro() ?  new Stack<AudioSource>(numLowpassInstances) : null;
			prefabSourcePool = new Dictionary<GameObject, Stack<AudioSource>>(32);

			// AudioSources are pooled to avoid excess garbage, so we allocate all of them up front.
			// For convenience, inactive AudioSources are parented to a pool object.
			HideFlags poolFlags = HideFlags.HideAndDontSave;
			GameObject sourcePoolParentObject = new GameObject("SourcePool");
			sourcePoolParentObject.hideFlags = poolFlags;
			sourcePoolParent = sourcePoolParentObject.transform;

			// Alloc simple instances
			for(int instanceIndex = 0; instanceIndex < numSimpleSources; ++instanceIndex)
			{
				GameObject newSourceObject = new GameObject("SimpleInstance" + instanceIndex);
				newSourceObject.hideFlags = poolFlags;
				newSourceObject.transform.parent = sourcePoolParent.transform;
				AudioSource newSource = newSourceObject.AddComponent<AudioSource>();
				newSource.playOnAwake = false;
				newSourceObject.SetActive(false);
				simpleSourcePool.Push(newSource);
			}

			// Alloc lowpass instances
			for(int instanceIndex = 0; instanceIndex < numLowpassInstances; ++instanceIndex)
			{
				GameObject newSourceObject = new GameObject("LowpassInstance" + instanceIndex);
				newSourceObject.hideFlags = poolFlags;
				newSourceObject.transform.parent = sourcePoolParent.transform;
				AudioSource newSource = newSourceObject.AddComponent<AudioSource>();
				newSource.playOnAwake = false;
				AudioLowPassFilter newLowpass = newSourceObject.AddComponent<AudioLowPassFilter>();
				newLowpass.enabled = false;
				newSourceObject.SetActive(false);
				lowpassSourcePool.Push(newSource);
			}

			ambienceStack = new List<SECTR_AudioAmbience>(32);
			activeInstances = new List<Instance>(MaxInstances);
			maxInstancesTable = new Dictionary<SECTR_AudioCue, int>(MaxInstances / 8);
			proximityTable = new Dictionary<SECTR_AudioCue, List<Instance>>(MaxInstances / 8);

			// Init the rest of our internal members.
			_UpdateTime();
			cachedMember = GetComponent<SECTR_Member>();
			windowHDRMax = HDRBaseLoudness;
			windowHDRMin = windowHDRMax - HDRWindowSize;
			occlusionPath = new List<SECTR_Graph.Node>(32);

			if(MasterBus != null)
			{
				MasterBus.ResetUserVolume();
				_UpdateBusPitchVolume(MasterBus, 1f, 1f);
			}
			else
			{
				Debug.LogWarning("SECTR AudioSystem has no MasterBus. Game sounds will not play.");
			}

#if UNITY_EDITOR
			auditionCue = ScriptableObject.CreateInstance<SECTR_AudioCue>();
			auditionCue.Spatialization = SECTR_AudioCue.Spatializations.Simple2D;
			auditionCue.Loops = false;
			soloBuses = null;
			oldOcclusionFlags = OcclusionFlags;
			if(!HUDLineMaterial)
			{
                string path = SECTR_SectorUtils.GetSectrDirectory() + SECTR_Constants.PATH_AudioHUDGraphMaterial;
                HUDLineMaterial = (Material)(AssetDatabase.LoadAssetAtPath(path, typeof(Material)));
			}
			EditorApplication.update += LateUpdate;
#endif
		}
	}

	void OnDisable()
	{
		if(audioSystem == this)
		{
			// Tear down the pools.
			int numActive = activeInstances.Count;
			for(int instanceIndex = 0; instanceIndex < numActive; ++instanceIndex)
			{
				Instance instance = activeInstances[instanceIndex];
				instance.Stop(true);
			}

			if(sourcePoolParent)
			{
#if UNITY_EDITOR
				if(Application.isEditor && !Application.isPlaying)
				{
					GameObject.DestroyImmediate(sourcePoolParent.gameObject);
				}
				else
#endif
				{
					GameObject.Destroy(sourcePoolParent.gameObject);
				}
				sourcePoolParent = null;
			}

			// Clear out all internals, to make it clear we're really shutdown.
			audioSystem = null;
			activeInstances = null;
			maxInstancesTable = null;
			proximityTable = null;
			instancePool = null;
			simpleSourcePool = null;
			lowpassSourcePool = null;
			prefabSourcePool = null;

			currentTime = 0;

			ambienceStack = null;
			currentAmbience = null;
			nextAmbienceOneShotTime = 0;
			
			currentMusic = null;

			cachedMember = null;

			occlusionPath = null;

			#if UNITY_EDITOR
			if(Application.isEditor && !Application.isPlaying)
			{
				ScriptableObject.DestroyImmediate(auditionCue);
			}
			else
			{
				ScriptableObject.Destroy(auditionCue);
			}
			auditionCue = null;
			soloBuses = null;
			oldOcclusionFlags = 0;
			EditorApplication.update -= LateUpdate;
			#endif
		}
	}

	// AudioSystem updates late to make sure it has latest from all gameplay and audio components.
	void LateUpdate()
	{
		if(audioSystem == this && !AudioListener.pause && MasterBus)
		{
#if UNITY_EDITOR
			if(OcclusionFlags != oldOcclusionFlags)
			{
				foreach(Instance instance in activeInstances)
				{
					instance.Reset();
				}
				oldOcclusionFlags = OcclusionFlags;
			}
#endif

			float deltaTime = _UpdateTime();

			// Update all bus data one up front for easier access later.
			_UpdateBusPitchVolume(MasterBus, 1f, 1f);

			_UpdateAmbience();

			// Update the HDR window from last frame before instanes update.
			windowHDRMax = Mathf.Max(HDRBaseLoudness, windowHDRMax - (HDRDecay * deltaTime));
			windowHDRMin = windowHDRMax - HDRWindowSize; 
			currentLoudness = 0;

			#if AUDIO_HUD
			int hdrIndex = hdrVolumeIndex % maxHDRVolumes;
			hdrVolumes[hdrIndex] = windowHDRMax;
			++hdrVolumeIndex;
			activeHDRSounds = false;
			#endif

			// Update all of the instances.
			int numActiveInstances = activeInstances.Count;
			int instanceIndex = 0;
			while(instanceIndex < numActiveInstances)
			{
				Instance instance = activeInstances[instanceIndex];
				instance.Update(deltaTime, false);
				if(!instance.Active && !instance.FadingOut)
				{
                    // Any instances that are inactive after update are recycled.
                    if (instance.KeepAliveTimeStamp == 0)
                    {
                        instance.KeepAliveTimeStamp = GetUnixTimeStamp();
                    }

                    if (GetUnixTimeStamp() >= instance.KeepAliveTimeStamp + instance.Cue.KeepAliveTime)
                    {
                        instance.Uninit();
                        activeInstances.RemoveAt(instanceIndex);
                        instancePool.Push(instance);
                        --numActiveInstances;
                    }
                    else
                    {
                        #if AUDIO_HUD
                        activeHDRSounds |= instance.HDR;
                        #endif
                        ++instanceIndex;
                    }
                    
                    
                }
				else
				{
					#if AUDIO_HUD
					activeHDRSounds |= instance.HDR;
					#endif
					++instanceIndex;
				}
			}
			// Apply the frame's loudness to the HDR window.
			currentLoudness = 10f * Mathf.Log10(currentLoudness);
			windowHDRMax = Mathf.Max(currentLoudness, windowHDRMax);
		}
	}

	#if AUDIO_HUD
	void OnGUI()
	{
		if(lineRenderer)
		{
			lineRenderer.enabled = ShowAudioHUD;
		}

		if(ShowAudioHUD && (!Application.isEditor || Application.isPlaying))
		{
			GUIStyle hudStyle = new GUIStyle(GUI.skin.label);

			const float border = 25f;
			const float indent = 10f;
			float textTop = (activeHDRSounds ? Screen.height * 0.2f : border);
			float itemWidth = Screen.width * 0.25f;
			float itemHeight = 20f;
			int numActive = activeInstances.Count;
			Vector2 cursorPos = new Vector2(border, textTop);
			GUI.Label(new Rect(cursorPos.x, cursorPos.y, itemWidth, itemHeight), "Bus Hierarchy", hudStyle);
			cursorPos.y += itemHeight;
			if(MasterBus != null)
			{
				DrawBusLabel(MasterBus, cursorPos, itemWidth, itemHeight, indent, border, hudStyle);
			}

			hudStyle.normal.textColor = Color.white;
			cursorPos.x = Screen.width - itemWidth - border;
			cursorPos.y = textTop;
			GUI.Label(new Rect(cursorPos.x, cursorPos.y, itemWidth, itemHeight), "Active Instances", hudStyle);
			cursorPos.x += indent;
			cursorPos.y += itemHeight;
			hudInstances.Clear();
			for(int activeIndex = 0; activeIndex < numActive && cursorPos.y < Screen.height - border; ++activeIndex)
			{
				Instance instance = activeInstances[activeIndex];
				if(!hudInstances.ContainsKey(instance.Cue))
				{
					int numInstances = 1;
					maxInstancesTable.TryGetValue(instance.Cue, out numInstances);
					GUI.Label(new Rect(cursorPos.x, cursorPos.y, itemWidth, itemHeight), instance.Cue.name + ":\t" + numInstances, hudStyle);
					hudInstances[instance.Cue] = instance.Cue;
					cursorPos.y += itemHeight;
				}
			}

			if(activeHDRSounds && HUDLineMaterial)
			{
				GUI.Label(new Rect(border, border, Screen.width * 0.5f, itemHeight), "HDR Loudness");

				if(!lineRenderer)
				{
					GameObject newObject = new GameObject("Audio Line Renderer");
					newObject.transform.parent = Camera.main.transform;
					newObject.transform.localPosition = Vector3.zero;
					newObject.transform.localRotation = Quaternion.identity;
					newObject.hideFlags = HideFlags.HideAndDontSave;
					lineRenderer = newObject.AddComponent<LineRenderer>();
					lineRenderer.useWorldSpace = false;
					lineRenderer.positionCount = maxHDRVolumes;
					lineRenderer.startWidth = 0.001f;
					lineRenderer.endWidth = 0.001f;
					lineRenderer.startColor = Color.white;
					lineRenderer.endColor = Color.white;
					lineRenderer.material = HUDLineMaterial;
					lineRenderer.material.color = Color.white;

				}

				float depth = Camera.main.nearClipPlane * 1.1f;
				float height = Mathf.Tan(Mathf.Deg2Rad * (Camera.main.fieldOfView * 0.5f)) * depth * 2f;
				float width = height * Camera.main.aspect;
				float startX = width * -0.45f;
				float endX = width * 0.45f;
				float startY = height * 0.3f;
                float endY = height * 0.75f;
				for(int volumeIndex = 0; volumeIndex < maxHDRVolumes; ++volumeIndex)
				{
					int realIndex = (hdrVolumeIndex + volumeIndex) % maxHDRVolumes;
					float alpha = volumeIndex / (float)maxHDRVolumes;
					float sampleX = Mathf.Lerp(endX, startX, alpha);
					float sampleY = Mathf.Lerp(startY, endY, hdrVolumes[realIndex] / (200f - HDRBaseLoudness));
					lineRenderer.SetPosition(volumeIndex, new Vector3(sampleX, sampleY, depth));
				}
			}
		}
	}

	float DrawBusLabel(SECTR_AudioBus bus, Vector2 cursorPos, float itemWidth, float itemHeight, float indent, float border, GUIStyle hudStyle)
	{
		if(bus != null)
		{
			cursorPos.x += indent;
			GUI.Label(new Rect(cursorPos.x, cursorPos.y, itemWidth, itemHeight), bus.name + ": " + bus.Volume.ToString("N2") + " (" + bus.EffectiveVolume.ToString("N2") + ")", hudStyle);
			cursorPos.y += itemHeight;
			int numChildren = bus.Children.Count;
			for(int childIndex = 0; childIndex < numChildren && cursorPos.y < Screen.height - border; ++childIndex)
			{
				hudStyle.normal.textColor = bus.Muted ? Color.gray : Color.white;
				cursorPos.y = DrawBusLabel(bus.Children[childIndex], cursorPos, itemWidth, itemHeight, indent, border, hudStyle);
			}
			cursorPos.x -= indent;
		}
		return cursorPos.y;
	}
	#endif
	#endregion
	
	#region Private Methods
	// Returns true if we're within our instance limit. 
	private static bool _CheckInstances(SECTR_AudioCue audioCue, bool isPlaying)
	{
		int maxInstances = audioCue.SourceCue.MaxInstances;
		// If this call is from an active instance, assume that it's one of the active
		// instances and don't count it against us.
		if(isPlaying)
		{
			maxInstances += 1;
		}
		if(maxInstances > 0)
		{
			int currentInstances;
			if(maxInstancesTable.TryGetValue(audioCue, out currentInstances) &&
			   currentInstances >= maxInstances)
			{
				return false;
			}
		}
		return true;
	}

	// Returns true if we're within our max distance and proximity limits.
	// Written to allow it to be called before an Instance is allocated.
	private static bool _CheckProximity(SECTR_AudioCue audioCue, Transform parent, Vector3 position, Instance testInstance)
	{
		if(parent)
		{
			position = parent.localToWorldMatrix.MultiplyPoint3x4(position);
		}
		SECTR_AudioCue srcCue = audioCue.SourceCue;
		float bufferDistance = srcCue.MaxDistance + audioSystem.CullingBuffer;
		if(Vector3.SqrMagnitude(position - Listener.position) <= (bufferDistance * bufferDistance))
		{
			int proximityLimit = srcCue.ProximityLimit;
			if(proximityLimit > 0)
			{
				List<Instance> instances;
				if(proximityTable.TryGetValue(audioCue, out instances))
				{
					int numInstances = instances.Count;
					if(numInstances > proximityLimit)
					{
						float sqrDistance = srcCue.MaxDistance + srcCue.MaxDistance;
						int hits = 0;
						for(int instanceIndex = 0; instanceIndex < numInstances; ++instanceIndex)
						{
							Instance instance = instances[instanceIndex];
							if(instance != testInstance && Vector3.SqrMagnitude(position - instance.Position) < sqrDistance)
							{
								if(++hits >= proximityLimit)
								{
									return false;
								}
							}
						}
					}
				}
			}

			return true;
		}

		return false;
	}

    private static double GetUnixTimeStamp()
    {
        return (System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1))).TotalSeconds;
    }

	private static float _UpdateTime()
	{
		float newTime = (float)AudioSettings.dspTime;
		float deltaTime = (newTime - currentTime);
		currentTime = newTime;
		return deltaTime;
	}

	// Recursive function that updates the effective volume and pitch of each bus.
	// After this is called, no one needs to walk the Bus hierarchy to get accurate
	// pitch or volume.
	private static void _UpdateBusPitchVolume(SECTR_AudioBus bus, float effectiveVolume, float effectivePitch)
	{
		if(bus)
		{
#if UNITY_EDITOR
            // Editor audio playback is a bit tricky, so match the default behavior here.
#if UNITY_2019_1_OR_NEWER
            if (Application.isEditor && !Application.isPlaying && !audioSystem.Debugging && SceneView.lastActiveSceneView && !SceneView.lastActiveSceneView.audioPlay)
#else
            if (Application.isEditor && !Application.isPlaying && !audioSystem.Debugging && SceneView.lastActiveSceneView && !SceneView.lastActiveSceneView.m_AudioPlay)
#endif
            {
                effectiveVolume = 0f;
			}

			if(soloBuses != null && soloBuses.Count > 0 && !soloBuses.Contains(bus) && bus != audioSystem.MasterBus)
			{
				effectiveVolume = 0;
			}
#endif
                bus.EffectiveVolume = effectiveVolume;
			bus.EffectivePitch = effectivePitch;
			int numChildren = bus.Children.Count;
			for(int childIndex = 0; childIndex < numChildren; ++childIndex)
			{
				_UpdateBusPitchVolume(bus.Children[childIndex], bus.EffectiveVolume, bus.EffectivePitch);
			}
		}
	}

	// Updates the ambience stack, plays one shots, etc.
	private static void _UpdateAmbience()
	{
		SECTR_AudioAmbience newAmbience = ambienceStack.Count > 0 ? ambienceStack[ambienceStack.Count - 1] : audioSystem.DefaultAmbience;
		// Handle changes in the active ambience.
		if(newAmbience != currentAmbience)
		{
			ambienceLoop.Stop(false);
			ambienceOneShot.Stop(false);

			currentAmbience = newAmbience;
			if(currentAmbience != null)
			{
				if(currentAmbience.OneShots.Count > 0)
				{
					nextAmbienceOneShotTime = currentTime + Random.Range(currentAmbience.OneShotInterval.x, currentAmbience.OneShotInterval.y);
				}

				if(currentAmbience.BackgroundLoop)
				{
					if(currentAmbience.BackgroundLoop.Spatialization == SECTR_AudioCue.Spatializations.Infinite3D)
					{
						ambienceLoop = Play(currentAmbience.BackgroundLoop, Listener, Random.onUnitSphere , true);
					}
					else
					{
						ambienceLoop = Play(currentAmbience.BackgroundLoop, Listener, Vector3.zero, true);
					}
				}
			}
		}

		// Generate one-shots, if applicable.
		if(currentAmbience != null)
		{
			int numOneShots = currentAmbience.OneShots.Count;
			if(numOneShots > 0 && currentTime >= nextAmbienceOneShotTime)
			{
				SECTR_AudioCue nextAmbienceOneShot = null;
				if(currentAmbience.UseOneShotCuesProbability)
				{
					// this is a little expensive, but it wont happen often
					if(currentAmbience.TotalProbability == 0.0f)
					{
						for(int ambienceIndex = 0; ambienceIndex < numOneShots; ++ambienceIndex)
						{
							SECTR_AudioCue oneShot = currentAmbience.OneShots[ambienceIndex];
							if(oneShot)
							{
								currentAmbience.TotalProbability += oneShot.PlayProbability;
							}
						}
					}
					
					float rand = Random.value * currentAmbience.TotalProbability;
					float totalProb = 0.0f;
					for(int oneShotIndex = 0; oneShotIndex < numOneShots; ++oneShotIndex)
					{
						SECTR_AudioCue oneShot = currentAmbience.OneShots[oneShotIndex];
						if(oneShot)
						{
							totalProb += oneShot.PlayProbability;
							if(totalProb > rand)
							{
								nextAmbienceOneShot = oneShot;
								break;
							}
						}
					}
				}
				else
				{
					//just choose any
					nextAmbienceOneShot = currentAmbience.OneShots[Random.Range(0, numOneShots)];
				}

				if(nextAmbienceOneShot != null)
				{
					if(nextAmbienceOneShot.SourceCue.Loops)
					{
						Debug.LogWarning("Cannot play ambient one shot " + nextAmbienceOneShot.name + ". It is set to loop.");
					}
					else
					{
						if(!nextAmbienceOneShot.IsLocal)
						{
							Debug.LogWarning("Ambient one shot " + nextAmbienceOneShot.name + "should be 2D or Infinite 3D.");
						}
						ambienceOneShot = Play(nextAmbienceOneShot, Listener, Random.onUnitSphere, false);
					}
				}
				nextAmbienceOneShotTime = currentTime + Random.Range(currentAmbience.OneShotInterval.x, currentAmbience.OneShotInterval.y);
			}

			if(ambienceLoop)
			{
				ambienceLoop.Volume = currentAmbience.Volume;
			}
			if(ambienceOneShot)
			{
				ambienceOneShot.Volume = currentAmbience.Volume;
			}
		}
	}

	// Recursive function to find a Bus by name.
	private static SECTR_AudioBus _FindBus(SECTR_AudioBus bus, string busName)
	{
		if(bus)
		{
			if(bus.name == busName)
			{
				return bus;
			}
			else
			{
				int numChildren = bus.Children.Count;
				for(int childIndex = 0; childIndex < numChildren; ++childIndex)
				{
					SECTR_AudioBus foundBus = _FindBus(bus.Children[childIndex], busName);
					if(foundBus)
					{
						return foundBus;
					}
				}
			}
		}
		return null;
	}
	#endregion
}
