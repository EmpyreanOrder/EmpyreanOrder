// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// An abstract base class for all components in SECTR Audio can be placed within
/// the scene. AudioSource also provides a common interface to the user, and basic 
/// functions like play, stop, etc.
/// 
/// It's important to note that AudioSource is not intendent to be the primary mechanism
/// by which sounds are played, merely a convenient way to place sounds in the world, and
/// in some cases trigger them from other built-in Unity features (like animation events).
/// In generally, programmers wishing to play sounds based on game events, should so so
/// by directly calling SECTR_AudioSystem.Play().
public abstract class SECTR_AudioSource : MonoBehaviour 
{
	#region Private Details
	[SerializeField] [HideInInspector] protected float volume = 1f;
	[SerializeField] [HideInInspector] protected float pitch = 1f;
	private bool playedFromStart = false;
	#endregion
	
	#region Public Interface
	[SECTR_ToolTip("The Cue to play from this source.", null, false)]
	public SECTR_AudioCue Cue = null;
	[SECTR_ToolTip("If the Cue should be forced to loop when playing.")]
	public bool Loop = true;
	[SECTR_ToolTip("Should the Cue auto-play when created.")]
	public bool PlayOnStart = true;

	public float Volume
	{
		get { return volume; }
		set 
		{
			if(volume != value)
			{
				volume = Mathf.Clamp01(value);
				OnVolumePitchChanged();
			}
		}
	}

	public float Pitch
	{
		get { return pitch; }
		set 
		{
			if(pitch != value)
			{
				pitch = Mathf.Clamp(value, 0f, 2f);
				OnVolumePitchChanged();
			}
		}
	}

	/// Returns true if the NoiseMaker is currently playing a sound.
	public abstract bool IsPlaying { get; }
	
	/// Make some noise! Plays the Cue. 
	public abstract void Play();

	/// Stops the Source from playing.
	/// <param name="stopImmediately">When true, overrides any fade out time set in the Cue.</param> 
	public abstract void Stop(bool stopImmediately);

	/// Hook to play sources from animation events.
	public void PlayEvent()
	{
		Play();
	}

	/// Hook to stop sources from animation events.
	/// <param name="stopImmediately">When true, overrides any fade out time set in the Cue.</param> 
	public void StopEvent(bool stopImmediately)
	{
		Stop(stopImmediately);
	}
	#endregion

	#region Unity Interface
	void Start()
	{
		if(PlayOnStart && Application.isPlaying)
		{
			Play();
			playedFromStart = true;
		}
	}

	void OnEnable()
	{
		if(PlayOnStart && playedFromStart && Application.isPlaying)
		{
			Play();
		}
	}

	protected virtual void OnDisable()
	{
		Stop(true);
	}
	#endregion

	#region Subclass Interface
	protected abstract void OnVolumePitchChanged();
	#endregion
}
