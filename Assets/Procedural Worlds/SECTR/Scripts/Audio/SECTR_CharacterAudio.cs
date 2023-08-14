// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

/// \ingroup Audio
/// Plays audio based on character events.
/// 
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Character Audio")]
public class SECTR_CharacterAudio : MonoBehaviour 
{
	#region Private Details
	private Dictionary<PhysicMaterial, SurfaceSound> surfaceTable = null;
	#endregion

	#region Public Interface
	[System.Serializable]
	public class SurfaceSound
	{
		[SECTR_ToolTip("The material that this set applies to.")]
		public PhysicMaterial SurfaceMaterial = null; 
		[SECTR_ToolTip("Default footstep sound. Used if no material specific sound exists.")]
		public SECTR_AudioCue FootstepCue = null;
		[SECTR_ToolTip("Default footstep sound. Used if no material specific sound exists.")]
		public SECTR_AudioCue JumpCue = null;
		[SECTR_ToolTip("Default landing sound. Used if no material specific sound exists.")]
		public SECTR_AudioCue LandCue = null;
	}

	[SECTR_ToolTip("Default sounds to play if there is no material specific sound.")]
	public SurfaceSound DefaultSounds = new SurfaceSound();
	[SECTR_ToolTip("List of surface specific sounds.")]
	public List<SurfaceSound> SurfaceSounds = new List<SurfaceSound>();
	#endregion

	#region UnityInterface
	void OnEnable()
	{	
		int numSurfaces = SurfaceSounds.Count;
		for(int surfaceIndex = 0; surfaceIndex < numSurfaces; ++surfaceIndex)
		{
			SurfaceSound surfaceSound = SurfaceSounds[surfaceIndex];
			if(surfaceSound.SurfaceMaterial != null)
			{
				if(surfaceTable == null)
				{
					surfaceTable = new Dictionary<PhysicMaterial, SurfaceSound>();
				}
				surfaceTable[surfaceSound.SurfaceMaterial] = surfaceSound;
			}
		}
	}

	void OnDisable()
	{
		surfaceTable = null;
	}
	#endregion

	#region CharacterMotor Interface
	void OnFootstep(PhysicMaterial currentMaterial)
	{
		SurfaceSound surfaceSound = _GetCurrentSurface(currentMaterial);
		SECTR_AudioSystem.Play(surfaceSound.FootstepCue, transform.position, false);
	}

	void OnJump(PhysicMaterial currentMaterial)
	{
		SurfaceSound surfaceSound = _GetCurrentSurface(currentMaterial);
		SECTR_AudioSystem.Play(surfaceSound.JumpCue, transform.position, false);
	}

	void OnLand(PhysicMaterial currentMaterial)
	{
		SurfaceSound surfaceSound = _GetCurrentSurface(currentMaterial);
		SECTR_AudioSystem.Play(surfaceSound.LandCue, transform.position, false);
	}
	#endregion

	#region Private Methods
	private SurfaceSound _GetCurrentSurface(PhysicMaterial currentMaterial)
	{		 
		SurfaceSound surfaceSound;
		if(currentMaterial != null && surfaceTable != null && surfaceTable.TryGetValue(currentMaterial, out surfaceSound))
		{
			return surfaceSound;
		}
		else
		{
			return DefaultSounds;
		}
	}
	#endregion
}
