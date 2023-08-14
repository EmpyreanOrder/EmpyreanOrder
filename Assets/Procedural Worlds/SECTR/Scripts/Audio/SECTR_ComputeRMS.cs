// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;

/// \ingroup Audio
/// Internal class to compute per-second RMS values of sounds and store
/// them in HDR keys.
[RequireComponent(typeof(AudioSource))]
[ExecuteInEditMode]
[AddComponentMenu("")]
public class SECTR_ComputeRMS : MonoBehaviour 
{
	#region Private Details
	private struct BakeInfo
	{
		public BakeInfo(SECTR_AudioCue cue, SECTR_AudioCue.ClipData clipData)
		{
			this.cue = cue;
			this.clipData = clipData;
		}
		
		public SECTR_AudioCue cue;
		public SECTR_AudioCue.ClipData clipData;
	}

	// bake master members
	private List<BakeInfo> hdrBakeList = null;
	private List<SECTR_ComputeRMS> activeBakeList = new List<SECTR_ComputeRMS>();
	private int hdrBakeIndex = 0;
	// invididual bakeer members
	private SECTR_AudioCue cue = null;
	private SECTR_AudioCue.ClipData clipData = null;
	private List<float> samples = new List<float>();
	private int numChannels = 0;
	#endregion

	#region public Interface
	#if UNITY_EDITOR
	/// Bakes HDR keys for the indicated list of cues.
	/// <returns>The master object coodinating the baking.</returns>
	/// <param name="cues">The cues to bake. Only HDR cues in this list will be baked.</param>
	static public SECTR_ComputeRMS BakeList(List<SECTR_AudioCue> cues)
	{
		List<BakeInfo> hdrBakeList = new List<BakeInfo>();
		int numCues = cues.Count;
		for(int cueIndex = 0; cueIndex < numCues; ++cueIndex)
		{
			SECTR_AudioCue cue = cues[cueIndex];
			if(cue.SourceCue.HDR)
			{
				int numClips = cue.AudioClips.Count;
				for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
				{
					SECTR_AudioCue.ClipData clipData = cue.AudioClips[clipIndex];
					if(!clipData.HDRKeysValid())
					{
						hdrBakeList.Add(new BakeInfo(cue, clipData));
					}
				}
			}
		}
		if(hdrBakeList.Count > 0)
		{
			hdrBakeList.Sort(delegate(BakeInfo a, BakeInfo b)
			{
				if(a.clipData.Clip.length == b.clipData.Clip.length)
				{
					return 0;
				}
				else
				{
					return a.clipData.Clip.length > b.clipData.Clip.length ? -1 : 1;
				}
			});
			GameObject bakeObject = new GameObject("Bake Master");
			bakeObject.hideFlags = HideFlags.HideAndDontSave;
			SECTR_ComputeRMS computeRMS = bakeObject.AddComponent<SECTR_ComputeRMS>();
			computeRMS.hdrBakeList = hdrBakeList;
			return computeRMS;
		}
		else
		{
			Debug.Log("All HDR Cues already baked.");
		}
		return null;
	}
	#endif

	/// Returns the progress of the current bake, from 0 to 1.
	public float Progress
	{
		get
		{
			if(hdrBakeList != null)
			{
				int numBakeItems = hdrBakeList.Count;
				int numActiveItems = activeBakeList.Count;
				float prevProgress = (hdrBakeIndex - numActiveItems) / (float)numBakeItems;
				float nextProgress = hdrBakeIndex / (float)numBakeItems;
				float leastProgress = 1f;
				for(int bakeIndex = 0; bakeIndex < numActiveItems; ++bakeIndex)
				{
					SECTR_ComputeRMS computeRMS = activeBakeList[bakeIndex];
					if(computeRMS)
					{
						leastProgress = Mathf.Min(leastProgress, computeRMS.Progress);
					}
				}
				return Mathf.Lerp(prevProgress, nextProgress, leastProgress);
			}
			else
			{
				AudioSource audioSource = GetComponent<AudioSource>();
				if(audioSource)
				{
					return audioSource.time / audioSource.clip.length;
				}
			}
			return 1f;
		}
	}
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		#if UNITY_EDITOR
		EditorApplication.update += Update;
		#endif
	}

	void OnDisable()
	{
		#if UNITY_EDITOR
		EditorApplication.update -= Update;
		#endif
	}

	void Update()
	{
		bool finished = false;
		if(hdrBakeList != null)
		{
			int numBakeCues = hdrBakeList.Count;
			finished = numBakeCues == 0;
			if(!finished)
			{
				if(activeBakeList.Count == 0)
				{
					if(hdrBakeIndex == numBakeCues)
					{
						#if UNITY_EDITOR
						AssetDatabase.SaveAssets();
						#endif
						finished = true;
					}
					else
					{
						const int maxSimultaneousBake = 4;
						int limit = Mathf.Min(hdrBakeIndex + maxSimultaneousBake, numBakeCues);
						for(int bakeIndex = hdrBakeIndex; bakeIndex < limit; ++bakeIndex)
						{
							BakeInfo bakeInfo = hdrBakeList[bakeIndex];
							GameObject bakeObject = new GameObject("Bake " + bakeInfo.cue.name + bakeInfo.clipData.Clip.name);
							bakeObject.transform.parent = transform;
							bakeObject.transform.localPosition = Vector3.zero;
							bakeObject.hideFlags = HideFlags.HideAndDontSave;
							SECTR_ComputeRMS computeRMS = bakeObject.AddComponent<SECTR_ComputeRMS>();
							computeRMS._StartCompute(bakeInfo.cue, bakeInfo.clipData); 
							activeBakeList.Add(computeRMS);
						}
						hdrBakeIndex = limit;
					}
				}
				else
				{
					bool allBaked = true;
					int bakeCount = activeBakeList.Count;
					for(int bakeIndex = 0; bakeIndex < bakeCount; ++bakeIndex)
					{
						if(activeBakeList[bakeIndex] != null)
						{
							allBaked = false;
							break;
						}
					}
					
					if(allBaked)
					{
						activeBakeList.Clear();
					}
				}
			}
		}
		else
		{
			int numSamples = samples.Count;
			finished = clipData == null;
			if(!finished && numSamples > 0)
			{
				int samplesPerSecond = AudioSettings.outputSampleRate * numChannels;
				int numCompleteSamples = (int)(clipData.Clip.length * samplesPerSecond);
				int sampleTolerance = samplesPerSecond / 10;
				AudioSource audioSource = GetComponent<AudioSource>();
				if((!audioSource.isPlaying && numSamples >= numCompleteSamples - sampleTolerance) ||
				   (audioSource.isPlaying && numSamples >= numCompleteSamples))
				{
					int numKeys = Mathf.CeilToInt((float)numSamples / (float)samplesPerSecond) + 1;
					float[] hdrKeys = new float[numKeys];
					int sampleIndex = 0;
					const float maxRange = 160f;
					for(int keyIndex = 1; keyIndex < numKeys; ++keyIndex)
					{
						float rms = 0f;
						int keySamples = 0;
						for(int windowIndex = 0; windowIndex < samplesPerSecond && sampleIndex < numSamples; ++windowIndex)
						{
							++keySamples;
							float sample = samples[sampleIndex++];
							rms += sample * sample;
						}
						rms = Mathf.Sqrt(rms/keySamples);
						// In some cases artifical silence is added at the end, so better to use the
						// previous, valid sample.
						if(Mathf.Abs(rms) < 0.001f && keyIndex == numKeys - 1 && numKeys > 2)
						{
							hdrKeys[keyIndex] = hdrKeys[keyIndex - 1];
						}
						else
						{
							// Convert to dB, with a reference of level of 0.1dB
							hdrKeys[keyIndex] = Mathf.Clamp(20f * Mathf.Log10(rms), -maxRange, maxRange);
						}
					}

					if(cue.SourceCue.Loops)
					{
						hdrKeys[0] = hdrKeys[hdrKeys.Length - 1];
					}
					else
					{
						hdrKeys[0] = hdrKeys[1];
					}

					// Check for a wav that's entirely silence. It's probably indicates there was a problem.
					bool allKeysZero = false;
					for(int keyIndex = 0; keyIndex < numKeys; ++keyIndex)
					{
						if(hdrKeys[keyIndex] > -maxRange)
						{
							allKeysZero = false;
							break;
						}
					}
					if(!allKeysZero)
					{
						#if UNITY_EDITOR
						clipData.SetHDRKeys(cue, hdrKeys);
						#endif
					}
					finished = true;
				}
				else if(!audioSource.isPlaying)
				{
					finished = true;
				}
			}
		}

		if(finished)
		{			
			#if UNITY_EDITOR
			if(Application.isEditor && !Application.isPlaying)
			{
				DestroyImmediate(gameObject);
			}
			else
			#endif
			{
				Destroy(gameObject);
			}
		}
	}

	void OnAudioFilterRead(float[] samples, int numChannels)
	{
		this.numChannels = numChannels;
		this.samples.AddRange(samples);
	}
	#endregion

	#region Private Methods
	public void _StartCompute(SECTR_AudioCue cue, SECTR_AudioCue.ClipData clipData)
	{
		this.cue = cue;
		this.clipData = clipData;
		AudioSource audioSource = GetComponent<AudioSource>();
		audioSource.clip = clipData.Clip;
		audioSource.dopplerLevel = 0;
		audioSource.ignoreListenerPause = true;
		audioSource.ignoreListenerVolume = true;
		audioSource.bypassListenerEffects = true;
		audioSource.bypassReverbZones = true;
		audioSource.maxDistance = float.MaxValue;
		audioSource.minDistance = float.MaxValue;
		audioSource.rolloffMode = AudioRolloffMode.Linear;
		audioSource.playOnAwake = false;
		audioSource.loop = false;
		audioSource.volume = 1;
		samples.Clear();
		GetComponent<AudioSource>().Play();
	}
	#endregion
}
