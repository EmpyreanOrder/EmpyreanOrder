// Copyright (c) 2014 Make Code Now! LLC
using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// Playes a SECTR_AudioCue when a trigger is activated.
/// 
/// TriggerSource supports any collider that Unity allows, 
/// provided it's marked to be a trigger.
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Trigger Source")]
public class SECTR_TriggerSource : SECTR_PointSource 
{
	#region Private Details
	GameObject activator = null;
	#endregion

	#region Public Interface
	public SECTR_TriggerSource()
	{
		Loop = false;
		PlayOnStart = false;
	}
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		// If we still have an activator, they must not have left,
		// So restore properly.
		if(!IsPlaying && activator)
		{
			Play();
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if(activator == null)
		{
			Play();
			activator = other.gameObject;
		}
	}
	
#if !EARLY_4
	void OnTriggerEnter2D(Collider2D other)
	{
		if(activator == null)
		{
			Play();
			activator = other.gameObject;
		}
	}
#endif

	void OnTriggerExit(Collider other)
	{
		if(activator == other.gameObject)
		{
			Stop(false);
			activator = null;
		}
	}
	
#if !EARLY_4
	void OnTriggerExit2D(Collider2D other)
	{
		if(activator == other.gameObject)
		{
			Stop(false);
			activator = null;
		}
	}
#endif
	#endregion
}
