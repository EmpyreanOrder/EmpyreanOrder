// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// Extends the basic SECTR_Door with sounds that play on state transitions.
/// 
/// There are four Cue's in this component, one for each state that the door can
/// be in. Like the animations for the door, the open and closed Cues will be
/// played looping, while the opening and closed cues are assumed to be one-shots.
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Door Audio")]
public class SECTR_DoorAudio : MonoBehaviour 
{
	#region Private Details
	private SECTR_AudioCueInstance instance;
	#endregion

	#region Public Interface
	[SECTR_ToolTip("Sound to play while door is in Open state.", null, false)]
	public SECTR_AudioCue OpenLoopCue = null;
	[SECTR_ToolTip("Sound to play while door is in Closed state.", null, false)]
	public SECTR_AudioCue ClosedLoopCue = null;
	[SECTR_ToolTip("Sound to play when door starts to open.", null, false)]
	public SECTR_AudioCue OpeningCue = null;
	[SECTR_ToolTip("Sound to play while door starts to close.", null, false)]
	public SECTR_AudioCue ClosingCue = null;
	[SECTR_ToolTip("Sound to play while waiting for the door to start opening.", null, false)]
	public SECTR_AudioCue WaitingCue = null;
#endregion

	#region Unity Interface
	void OnDisable()
	{
		_Stop(true);
	}
	#endregion

	#region Door Interface
	void OnOpen()
	{
		_Stop(false);
		instance = SECTR_AudioSystem.Play(OpenLoopCue, transform, Vector3.zero, true);
	}
	
	void OnOpening()
	{
		_Stop(false);
		instance = SECTR_AudioSystem.Play(OpeningCue, transform, Vector3.zero, false);
	}
	
	void OnClose()
	{
		_Stop(false);
		instance = SECTR_AudioSystem.Play(ClosedLoopCue, transform, Vector3.zero, true);
	}
	
	void OnClosing()
	{
		_Stop(false);
		instance = SECTR_AudioSystem.Play(ClosingCue, transform, Vector3.zero, false);
	}

	void OnWaiting()
	{
		_Stop(false);
		instance = SECTR_AudioSystem.Play(WaitingCue, transform, Vector3.zero, true);
	}
	#endregion

	#region Private Details
	private void _Stop(bool stopImmediately)
	{
		instance.Stop(stopImmediately);
	}
	#endregion

}
