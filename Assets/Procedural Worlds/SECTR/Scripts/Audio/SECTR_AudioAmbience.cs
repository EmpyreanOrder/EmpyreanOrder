// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

/// Defines the data specific to a particular SECTR_AudioEnvironment.
/// 
/// The goal of environmental audio (also known as ambient audio) is to create a base
/// layer of ambient sound effects, effects which always play even if there is not
/// much going on in the game.
/// 
/// In SECTR, AudioAmbiences contain two sets of cues that can help create this baseline.
/// Each AudioAmbience can have a background loop, which will be played
/// as a looping, 2D sound as long as that AudioAmbiences is the current, highest priority
/// AudioAmbiences in the scene. The AudioAmbiences can also have one or more one-shot
/// sounds that will fire randomly. If these one-shots are marked as Infinite3D,
/// then they will be randomly position in the surround field, giving the impression of
/// sounds playing all around the player.
[System.Serializable]
public class SECTR_AudioAmbience
{
	[SECTR_ToolTip("The looping 2D cue to play as long as this ambience is active.", null, false)]
	public SECTR_AudioCue BackgroundLoop = null;
	[SECTR_ToolTip("A list of one-shots that will play randomly around the listener.")]
	public List<SECTR_AudioCue> OneShots = new List<SECTR_AudioCue>();
	[SECTR_ToolTip("The min and max time between one-shot playback.", "OneShots")]
	public Vector2 OneShotInterval = new Vector2(30f, 60f);
	[SECTR_ToolTip("The a volume scalar for the Cues in this Ambience. Combines with the base Cue volume.")]
	public float Volume = 1f;
	[SECTR_ToolTip("When enabled the higher probability one shots will be more likely to play, gets the probability from the cue.")]
	public bool UseOneShotCuesProbability = true;
	[HideInInspector]
	public float TotalProbability=0.0f;
}