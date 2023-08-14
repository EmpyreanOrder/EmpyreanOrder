// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// Plays a SECTR_AudioCue at this point in the world.
/// 
/// Point Source is the SECTR Audio equivalent of Unity's
/// AudioSource component in that it simply plays a sound
/// at a point in space. Point Source, however, benefits
/// from the full set of creating, mixing, and other
/// advanced features of SECTR Audio, but is only barely
/// more expensive than a raw Unity AudioSource.
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Point Source")]
public class SECTR_PointSource : SECTR_AudioSource 
{
	#region Private Details
	protected SECTR_AudioCueInstance instance;
	#endregion

	#region Public Interface
	/// Returns true if the NoiseMaker is currently playing a sound.
	public override bool IsPlaying { get { return instance; } }

	/// Make some noise! Plays the Cue. 
	public override void Play()
	{
		if(Loop && IsPlaying)
		{
			instance.Stop(false);
		}
		
		if(Cue != null)
		{
			if(Cue.Spatialization == SECTR_AudioCue.Spatializations.Infinite3D)
			{
				instance = SECTR_AudioSystem.Play(Cue, SECTR_AudioSystem.Listener, Random.onUnitSphere, Loop);
			}
			else
			{
				instance = SECTR_AudioSystem.Play(Cue, transform, Vector3.zero, Loop);
			}
			if(instance)
			{
				instance.Volume = volume;
				instance.Pitch = pitch;
			}
		}
	}
	
	/// Stops the Source from playing.
	/// <param name="stopImmediately">Overrides any fade-out specified in the Cue</param>
	public override void Stop(bool stopImmediately)
	{
		instance.Stop(stopImmediately);
	}
	#endregion

	#region Audio Source Interface
	protected override void OnVolumePitchChanged()
	{
		if(instance)
		{
			instance.Volume = volume;
			instance.Pitch = pitch;
		}
	}
	#endregion
}
