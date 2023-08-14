// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

/// \ingroup Music
/// Plays a piece of music on Start.
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Start Music")]
public class SECTR_StartMusic : MonoBehaviour 
{
	#region Public Interface
	[SECTR_ToolTip("The music to play on Start.")]
	public SECTR_AudioCue Cue;
	#endregion
	
	#region Unity Interface
	void Start()
	{
		SECTR_AudioSystem.PlayMusic(Cue);
		GameObject.Destroy(this);
	}
	#endregion
}
