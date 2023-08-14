// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// Makes the specified music active when a trigger is entered.
/// 
/// TriggerSource supports any collider that Unity allows, 
/// provided it's marked to be a trigger.
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Music Trigger")]
public class SECTR_MusicTrigger : MonoBehaviour 
{
	#region Private Details
	GameObject activator = null;
	#endregion

	#region Public Interface
	[SECTR_ToolTip("The Cue to play as music. If null, this trigger will stop the current music.", null, false)]
	public SECTR_AudioCue Cue = null;
	[SECTR_ToolTip("Should music be forced to loop when playing.")]
	public bool Loop = true;
	[SECTR_ToolTip("Should the music stop when leaving the trigger.")]
	public bool StopOnExit = false;
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		// If we still have an activator, they must still be in the trigger,
		// So auto-restart.
		if(activator)
		{
			_Play();
		}
	}

	void OnDisable()
	{
		if(StopOnExit)
		{
			_Stop(false);
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if(activator == null)
		{
			if(Cue != null)
			{
				_Play();
			}
			else
			{
				_Stop(false);
			}
			activator = other.gameObject;
		}
	}

#if !EARLY_4
	void OnTriggerEnter2D(Collider2D other)
	{
		if(activator == null)
		{
			if(Cue != null)
			{
				_Play();
			}
			else
			{
				_Stop(false);
			}
			activator = other.gameObject;
		}
	}
#endif

	void OnTriggerExit(Collider other)
	{
		if(StopOnExit && other == activator)
		{
			_Stop(false);
			activator = null;
		}
	}
	
#if !EARLY_4
	void OnTriggerExit2D(Collider2D other)
	{
		if(StopOnExit && other.gameObject == activator)
		{
			_Stop(false);
			activator = null;
		}
	}
#endif
	#endregion

	#region Private Details
	private void _Play()
	{
		if(Cue != null)
		{
			SECTR_AudioSystem.PlayMusic(Cue);
		}
	}
	
	private void _Stop(bool stopImmediately)
	{
		SECTR_AudioSystem.StopMusic(stopImmediately);
	}
	#endregion
}
