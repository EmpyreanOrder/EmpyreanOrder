// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;

public interface SECTR_IAudioInstance
{
	int Generation				{ get; }
	bool Active 				{ get; }
	Vector3 Position			{ get; set; }
	Vector3 LocalPosition 		{ get; set; }  
	float Volume 				{ get; set; }
	float Pitch 				{ get; set; }
	bool Mute					{ get; set; }
	bool Pause					{ get; set; }
	int TimeSamples				{ get; set; }
	float TimeSeconds			{ get; set; }
	void Stop(bool stopImmediately);
	void ForceInfinite();
	void ForceOcclusion(bool occluded);
	void SetParameter(string param, float value);
	AudioSource GetInternalAudioSource();
}

/// \ingroup Audio
/// A handle to and interface for instances of SECTR_AudioCue.
///
/// A unique SECTR_AudioCueInstance is returned each time a SECTR_AudioCue is played.
/// This instance serves as a handle with which to manipulate the instance after initial playback
/// (if so desired). Client systems are free to ignore this return value, in which case the sound
/// is assumed to be "fire-and-forget". Looping sounds, however, will not auto-stop themselves
/// (until the end of the game) so programmers should take care to stop handle them properly.
/// </description>
public struct SECTR_AudioCueInstance : SECTR_IAudioInstance
{
	#region Private Details
	private SECTR_IAudioInstance internalInstance;
	private int generation;
	#endregion

	public SECTR_AudioCueInstance(SECTR_IAudioInstance internalInstance, int generation)
	{
		this.internalInstance = internalInstance;
		this.generation = generation;
	}

	public int Generation				
	{ 
		get { return generation; } 
	}

	/// Does this instance refer to an active, valid sound, or is it a dead handle.
	public bool Active 				
	{
		get { return internalInstance != null && (generation == internalInstance.Generation) && internalInstance.Active; }
	}
	
	/// Accessor for the instance's world space position.
	public Vector3 Position			
	{
		get { return Active ? internalInstance.Position : Vector3.zero; }
		set
		{
			if(Active)
			{
				internalInstance.Position = value;
			}
		}
	}
	
	/// Accessor for the instance's  local space position.
	public Vector3 LocalPosition
	{
		get { return Active ? internalInstance.LocalPosition : Vector3.zero; } 
		set
		{
			if(Active)
			{
				internalInstance.LocalPosition = value;
			}
		}
	}  
	
	/// Accessor for the volume of the instance. This volume will be combined with other volumes
	/// (like from the Bus hierarchy) to produce the final volume.
	public float Volume
	{
		get { return Active ? internalInstance.Volume : 0f; } 
		set
		{
			if(Active)
			{
				internalInstance.Volume = value;
			}
		}
	}
	
	/// Accessor for the pitch of the instance. This pitch will be combined with the pitch
	/// from the Bus hierarchy to produce the final pitch.
	public float Pitch
	{
		get { return Active ? internalInstance.Pitch : 1f; } 
		set
		{
			if(Active)
			{
				internalInstance.Pitch = value;
			}
		}
	}
	
	/// Accessor for the mute state of the instance. Mute state will be combined with the bus hierarchy mute
	/// state to produce a final volume.
	public bool Mute					
	{
		get { return Active ? internalInstance.Mute : false; } 
		set
		{
			if(Active)
			{
				internalInstance.Mute = value;
			}
		}
	}

	/// Accessor for the pause state of the instance. Pause state will be combined with the system
	/// pause state to produce a final value.
	public bool Pause					
	{
		get { return Active ? internalInstance.Pause : false; } 
		set
		{
			if(Active)
			{
				internalInstance.Pause = value;
			}
		}
	}

	/// Accessor for the elapsed playback time in seconds.
	public float TimeSeconds			
	{
		get { return Active ? internalInstance.TimeSeconds : 0f; } 
		set
		{
			if(Active)
			{
				internalInstance.TimeSeconds = value;
			}
		}
	}

	/// Accessor for the elapsed playback time in samples.
	public int TimeSamples				
	{
		get { return Active ? internalInstance.TimeSamples : 0; } 
		set
		{
			if(Active)
			{
				internalInstance.TimeSamples = value;
			}
		}
	}

	/// Ends playback of the specified audio instance.
	/// <param name="stopImmediately">Immediate stop will ignore any Fade Out set on the original SECTR_AudioCue.</param>
	public void Stop(bool stopImmediately)
	{
		if(Active)
		{
			internalInstance.Stop(stopImmediately);
		}
	}

	/// Forces a 3D sound to act as infinite 3D. For very special case uses.
	public void ForceInfinite()
	{
		if(Active)
		{
			internalInstance.ForceInfinite();
		}
	}
	
	/// Forces occlusion on or off. For very special use cases.
	public void ForceOcclusion(bool occluded)
	{
		if(Active)
		{
			internalInstance.ForceOcclusion(occluded);
		}
	}

	/// Sets the value of a control parameter
	public void SetParameter(string param, float value)
	{
		if(Active)
		{
			internalInstance.SetParameter(param, value);
		}
	}

	/// Returns the internal audio source, may be null. For advanced users only.
	public AudioSource GetInternalAudioSource() 
	{
		return Active ? internalInstance.GetInternalAudioSource() : null;
	}

	/// Returns internal instance. For very special use cases.
	public SECTR_IAudioInstance GetInternalInstance()
	{
		return internalInstance;
	}
	
	public static implicit operator bool(SECTR_AudioCueInstance x) 
	{
		return x.Active;
	}
}