// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

/// \ingroup Audio
/// A Cue is the atomic, playable object in SECTR Audio. It encapsulates
/// all of the data necessary for randomization, spatialization, mixing, etc.
/// 
/// Cue has a number of properties, but they fall into three basic categories:
/// properties common to all cues (like pitch and spatialization), spatial properties
/// (i.e. 2D or 3D specific attributes), and properties related to the management and
/// playback of AudioClips. These categories are visible in the code below, and in
/// the custom inspector in the Unity Editor.
///
/// Because games often have many sounds with the same properties but different AudioClips,
/// AudioCue provides a simple templating system. Templates are somewhat like a simple,
/// audio specific version of Unity prefabs, though they do not allow per-attribute overrides.
/// Because of this feature, programmers who need to access the properties of a given SoundCue
/// should be careful to use the SourceCue property as that will always return the AudioCue that
/// whose properties will be used by the SECTR_AudioSystem.
public class SECTR_AudioCue : ScriptableObject
{
	#region Private Details
	[SerializeField] [HideInInspector] private SECTR_AudioCue template;
	[SerializeField] [HideInInspector] private SECTR_AudioBus bus;
	
	private int clipPlaybackIndex = -1;
	private bool needsShuffling = true;
	private bool pingPongIncrement = true;

#if UNITY_EDITOR
	private int templateReferences = 0;
#endif
	#endregion

	#region Public Interface
	/// Types of rules for picking the next AudioClip. 
	public enum PlaybackModes
	{
		/// Select an AudioClip at random.
		Random,			
		/// Select an AudioClip at random, but do not repeat any until all have played.
		Shuffle,		
		/// Play AudioClips in order, starting over at the beginning when all are played.
		Loop,			
		/// Play AudioClips in ascending and then descending order.
		PingPong,		
	};

	/// Types of rules for picking the next AudioClip. 
	public enum FalloffTypes
	{
		/// Audio attenuates linearly between Min and Max distances.
		Linear,
		/// Audio attenuates logarithmic between Min and Max distances.
		Logarithmic,     
	};

	/// Ways to spatialize the sound (i.e. position in the surround field)
	public enum Spatializations
	{
		/// The most basic 2D sound. Not affected by 3D position at all.
		Simple2D,		
		/// A 3D sound with direction by no distance attenuation. Ideal for random ambient one shots.
		Infinite3D,		
		/// The most basic 3D sound. Spatialized and attenuated by 3D position.
		Local3D,		
		/// Same behavior as a 3D sound, but may be affected by Occlusion calculations.
		Occludable3D,	
	};
	
	[System.Serializable]
	public class ClipData
	{
		#region Private Details
		[SerializeField] private AudioClip clip = null;
		[SerializeField] private bool playedInShuffle = false;
		[SerializeField] private float volume = 1;
		[SerializeField] private SECTR_ULong bakeTimestamp = new SECTR_ULong(0);
		#endregion

		#region Public Interface
		public ClipData(AudioClip clip)
		{
			this.clip = clip;
			playedInShuffle = false;
			volume = 1;
		}

		public AudioClip Clip { get { return clip; } }
		public float Volume
		{
			get { return volume; } 
			set { volume = value; }
		}
		public bool PlayedInShuffle 
		{
			get { return playedInShuffle; }
			set { playedInShuffle = value; }
		}

		public AnimationCurve HDRCurve = null;

		#if UNITY_EDITOR
		public bool HDRKeysValid()
		{
			if(HDRCurve == null || HDRCurve.length == 0 || (Mathf.Abs(HDRCurve.keys[HDRCurve.length - 1].time - Mathf.CeilToInt(clip.length)) > 1f))
			{
				return false;
			}
			else
			{
				AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
				if(importer.assetTimeStamp > bakeTimestamp)
				{
					return false;
				}
			}
			return true;
		}

		public void SetHDRKeys(SECTR_AudioCue cue, float[] hdrKeys)
		{
			int numKeys = hdrKeys.Length;
			if(numKeys > 0)
			{
				HDRCurve = new AnimationCurve();
				for(int keyIndex = 0; keyIndex < numKeys; ++keyIndex)
				{
					if(keyIndex == 0 || keyIndex == numKeys - 1)
					{
						Keyframe newKey = new Keyframe(keyIndex, hdrKeys[keyIndex], 0f, 0f);
						HDRCurve.AddKey(newKey);
					}
					else
					{
						HDRCurve.AddKey(keyIndex, hdrKeys[keyIndex]);
					}
				}
				if(cue.Loops)
				{
					HDRCurve.preWrapMode = WrapMode.Loop;
					HDRCurve.postWrapMode = WrapMode.Loop;
				}
				AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
				this.bakeTimestamp.value = importer.assetTimeStamp;
				EditorUtility.SetDirty(cue);
			}
		}
		#endif
		#endregion
	}

	// Universal Properties
	[SECTR_ToolTip("List of Audio Clips for this Cue to choose from.")] 
	public List<ClipData> AudioClips = new List<ClipData>();
	[SECTR_ToolTip("The rules for selecting which audio clip to play next")]
	public PlaybackModes PlaybackMode = PlaybackModes.Random;
	[SECTR_ToolTip("Determines if the sound should be mixed in HDR or LDR.")]
	public bool HDR = false;
	[SECTR_ToolTip("The loudness, in dB(SPL), of this HDR Cue.")]
	public Vector2 Loudness = new Vector2(50f, 50f);
	[SECTR_ToolTip("The volume of this Cue.")]
	public Vector2 Volume = Vector2.one;
	[SECTR_ToolTip("The pitch adjustment of this Cue.")]
	public Vector2 Pitch = Vector2.one;
	[SECTR_ToolTip("Set to true to auto-loop this Cue.")]
	public bool Loops = false;
	[SECTR_ToolTip("Cue priority, lower is more important.", 0, 255)]
	public int Priority = 128;
	[SECTR_ToolTip("Prevent this Cue from recieving Audio Effects.")]
	public bool BypassEffects = false;
	[SECTR_ToolTip("Maximum number of instances of this Cue that can be played at once.", 1, -1)]
	public int MaxInstances = 10;
	[SECTR_ToolTip("Number of seconds over which to fade in the Cue when played.", 0, -1)]
	public float FadeInTime = 0f;
	[SECTR_ToolTip("Number of seconds over which to fade out the Cue when stopped.", 0, -1)]
	public float FadeOutTime = 0f;
    [SECTR_ToolTip("Number of seconds to keep this Audio Cue alive after it has stopped playing. (Used to allow effects like reverb etc. to play out before destroying the Audio Source.)")]
    public int KeepAliveTime = 0;
    [SECTR_ToolTip("Sets rules for how to spatialize this sound.")]
	public Spatializations Spatialization = Spatializations.Local3D;
	// 2D Properties
	[SECTR_ToolTip("Expands or narrows the range of speakers out of which this Cue plays.", 0f, 360f)]
	public float Spread = 0;
	[SECTR_ToolTip("Moves the sound around the speaker field.", -1f, 1f)]
	public float Pan2D = 0;
	// 3D Properties
	[SECTR_ToolTip("Attenuation style of this clip.")]
	public FalloffTypes Falloff = FalloffTypes.Linear;
	[SECTR_ToolTip("The range at which the sound is no longer audible.", 0, -1)]
	public float MaxDistance = 100;
	[SECTR_ToolTip("The range within which the sound will be at peak volume/loudness.", 0, -1)]
	public float MinDistance = 10;
	[SECTR_ToolTip("Scales the amount of doppler effect applied to this Cue.", 0f, 1f)]
	public float DopplerLevel = 0;
	[SECTR_ToolTip("Prevents too many instances of a cue playing near one another.", 0f, -1)]
	public int ProximityLimit = 0;
	[SECTR_ToolTip("The size of the proximity limit check.", "ProximityLimit", 0, -1)]
	public float ProximityRange = 10;
	[SECTR_ToolTip("Allows you to scale down the amount of occlusion applied to this Cue (when occluded).", 0f, 1f)]
	public float OcclusionScale = 1f;
	[SECTR_ToolTip("The chance that this cue will actually make a sound when played.", 0f, 1f)]
	public float PlayProbability = 1f;
	[SECTR_ToolTip("Random delay before start of playback.")]
	public Vector2 Delay = Vector2.zero;
	[SECTR_ToolTip("Special prefab to use when playing this Cue. Useful for adding effects to this source.", null, false)]
	public GameObject Prefab;
	[SECTR_ToolTip("Control parameters for this Cue")]
	public List<SECTR_CueParam> ControlParams = new List<SECTR_CueParam>();

	/// Accessor for the Template cue of this Cue. If set, the Template will override
	/// all properties of the Cue except for the list of AudioClips and the parent Bus. 
	public SECTR_AudioCue Template
	{
		set
		{
			if(template != value && value != this)
			{
				#if UNITY_EDITOR
				if(template)
				{
					template.RemoveTemplateRef();
				}
				#endif

				template = value;

				#if UNITY_EDITOR
				if(template)
				{
					template.AddTemplateRef();
				}
				EditorUtility.SetDirty(this);
				#endif
			}
		}
		get { return template; }
	}

	/// Accessor for the Bus of this Cue.
	public SECTR_AudioBus Bus
	{
		set
		{
			if(bus != value)
			{
				#if UNITY_EDITOR
				if(bus)
				{
					bus.RemoveCue(this);
				}
				#endif

				bus = value;

				#if UNITY_EDITOR
				if(bus)
				{
					bus.AddCue(this);
				}
				EditorUtility.SetDirty(this);
				#endif
			}
		}
		get { return bus; }
	}
	
	/// Returns the Cue that determines the 2D and 3D properties,
    /// will always be this Cue or its Template. 
	public SECTR_AudioCue SourceCue
	{
		get { return template != null ? template : this; }
	}

	/// Returns true if this Cue is Local3D or Infinite3D.
	public bool Is3D 
	{ 
		get { return Spatialization != Spatializations.Simple2D; } 
	}

	/// Returns true if this Cue is Simple2D or Infinite3D.
	public bool IsLocal
	{ 
		get { return Spatialization == Spatializations.Simple2D || Spatialization == Spatializations.Infinite3D; } 
	}

	/// Returns the index of the currently playing AudioClip.
	public int ClipIndex
	{
		get { return clipPlaybackIndex; }
	}

	/// Returns the next AudioClip to be played, as determined by the PlaybackMode.
	public ClipData GetNextClip()
	{
		int numClips = AudioClips.Count;
		if(numClips == 1)
		{
			return AudioClips[0];
		}
		else if(numClips > 0)
		{
			switch(PlaybackMode)
			{
			case PlaybackModes.Random:
				return AudioClips[Random.Range(0, numClips)];
			case PlaybackModes.Loop:
				clipPlaybackIndex = ++clipPlaybackIndex % numClips;
				return AudioClips[clipPlaybackIndex];
			case PlaybackModes.Shuffle:
				++clipPlaybackIndex;
				if(clipPlaybackIndex >= numClips)
				{
					clipPlaybackIndex = 0;
					needsShuffling = true;
				}
				if(needsShuffling)
				{
					_ShuffleClips();
					needsShuffling = false;
				}
				return AudioClips[clipPlaybackIndex];
			case PlaybackModes.PingPong:
				if(pingPongIncrement)
				{
					++clipPlaybackIndex;
					pingPongIncrement = (clipPlaybackIndex < AudioClips.Count - 1);
				}
				else
				{
					--clipPlaybackIndex;
					pingPongIncrement = (clipPlaybackIndex <= 0);
				}
				return AudioClips[clipPlaybackIndex];
			}
		}
		return null;
	}

	public float MinClipLength()
	{
		float minLength = float.MaxValue;
		bool hasClips = false;
		int numClips = AudioClips.Count;
		for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
		{
			AudioClip clip = AudioClips[clipIndex].Clip;
			if(clip)
			{
				minLength = Mathf.Min(minLength, clip.length);
				hasClips = true;
			}
		}
		return hasClips ? minLength : 0f;
	}
	
	public float MaxClipLength()
	{
		float maxLength = 0;
		int numClips = AudioClips.Count;
		for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
		{
			AudioClip clip = AudioClips[clipIndex].Clip;
			if(clip)
			{
				maxLength = Mathf.Max(maxLength, clip.length);
			}
		}
		return maxLength;
	}

	public void ResetClipIndex()
	{
		needsShuffling = true;
		pingPongIncrement = true;
		clipPlaybackIndex = -1;
	}

	#if UNITY_EDITOR
	public bool IsTemplate
	{
		get { return templateReferences > 0; }
	}

	public int RefCount
	{
		get { return templateReferences; }
	}

	public void AddTemplateRef()
	{
        //fix up potential legacy negative reference counts
        if (templateReferences < 0)
        {
            templateReferences = 0;
        }

		++templateReferences;
	}
	
	public void RemoveTemplateRef()
	{
		--templateReferences;

        //fix up potential legacy negative reference counts
        if (templateReferences < 0)
        {
            templateReferences = 0;
        }
    }

	public void AddClip(AudioClip clip, bool suppressWarnings)
	{
		if(clip != null)
		{
			if(HasClip(clip) && !suppressWarnings)
			{
				Debug.LogWarning("Cannot add the same clip more than once.");
				return;
			}

			AudioClips.Add(new ClipData(clip));
			EditorUtility.SetDirty(this);
		}

		CleanClips();
	}
	
	public void RemoveClip(AudioClip clip)
	{
		if(clip != null)
		{
			int numClips = AudioClips.Count;
			for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
			{
				if(AudioClips[clipIndex].Clip == clip)
				{
					RemoveClip(clipIndex);
					break;
				}
			}
		}

		CleanClips();
	}

	public void RemoveClip(int clipIndex)
	{
		if(clipIndex >= 0 && clipIndex < AudioClips.Count)
		{
			AudioClips.RemoveAt(clipIndex);
			EditorUtility.SetDirty(this);
		}

		CleanClips();
	}

	public bool HasClip(AudioClip clip)
	{
		int numClips = AudioClips.Count;
		for(int clipIndex = 0; clipIndex < numClips; ++clipIndex)
		{
			if(AudioClips[clipIndex].Clip == clip)
			{
				return true;
			}
		}
		return false;
	}

	// Wipes the list of AudioClips
	public void ClearClips()
	{
		AudioClips.Clear();
		ResetClipIndex();
		EditorUtility.SetDirty(this);
	}

	// Removes any NULL clips from the list, which can happen if, for instance,
	// a user deletes a resource without removing it from the Cue.
	public void CleanClips()
	{
		int clipIndex = 0;
		bool cleanedClip = false;
		while(clipIndex < AudioClips.Count)
		{
			if(AudioClips[clipIndex] == null)
			{
				AudioClips.RemoveAt(clipIndex);
				cleanedClip = true;
			}
			else
			{
				++clipIndex;
			}
		}

		ResetClipIndex();

		if(cleanedClip)
		{
			EditorUtility.SetDirty(this);
		}
	}
	#endif
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		ResetClipIndex();
#if UNITY_EDITOR
		if(template)
		{
			template.AddTemplateRef();
		}

		if(Bus != null)
		{
			Bus.AddCue(this);
		}
#endif
	}
	
	void OnDisable()
	{
#if UNITY_EDITOR
		if(template)
		{
			template.RemoveTemplateRef();
		}

		if(Bus != null)
		{
			Bus.RemoveCue(this);
		}
#endif
	}
	#endregion

	#region Private Methods
	private void _ShuffleClips()
	{
		System.Random rng = new System.Random();
		int n = AudioClips.Count;  
		while (n >= 1) {  
			n--;  
			int k = rng.Next(n + 1);  
			ClipData value = AudioClips[k];  
			AudioClips[k] = AudioClips[n];  
			AudioClips[n] = value;  
		}
	}
	#endregion


}
