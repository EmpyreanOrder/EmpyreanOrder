// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

/// \ingroup Audio
/// Activates a SECTR_AudioAmbience whenever a player enters
/// an AudioReverbZone.
/// 
/// Audio Reverb can be an important part of creating a believeable
/// Audio Environment. This component makes that easy, by ensuring that
/// the specified Audio Environment is always active whenever the Reverb
/// is audible. Because AudioReverbZone's are always spherical, the distance
/// check is very inexpensive. As with all SECTR_AudioEnvironment components, 
/// AudioEnvironmentZones can be overlapped and nested.
[RequireComponent(typeof(AudioReverbZone))]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Audio Environment Zone")]
public class SECTR_AudioEnvironmentZone : SECTR_AudioEnvironment
{
	#region Private Details
	private AudioReverbZone cachedZone = null;
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		cachedZone = GetComponent<AudioReverbZone>();
	}

	void OnDisable()
	{
		cachedZone = null;
		Deactivate();
	}

	void Update()
	{
		if(SECTR_AudioSystem.Initialized)
		{
			bool shouldBeActive = Vector3.SqrMagnitude(SECTR_AudioSystem.Listener.position - transform.position) <= (cachedZone.maxDistance * cachedZone.maxDistance);
			if(shouldBeActive != Active)
			{
				if(shouldBeActive)
				{
					Activate();
				}
				else
				{
					Deactivate();
				}
			}
		}
	}
	#endregion
}