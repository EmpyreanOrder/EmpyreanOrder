// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections;

//[AddComponentMenu("SECTR/Demos/SECDR Audio Demo UI")]
public class SECTR_AudioDemoUI : SECTR_DemoUI 
{
	#region Private details
	SECTR_AudioCueInstance chatterInstance;
	#endregion

	#region Public Interface
	[SECTR_ToolTip("A cue with a low loudness.", null, false)]
	public SECTR_AudioCue SoftCue;
	[SECTR_ToolTip("A cue at the normal loudness of speech.", null, false)]
	public SECTR_AudioCue ChatterCue;
	[SECTR_ToolTip("A loud sound.", null, false)]
	public SECTR_AudioCue GunshotCue;
	[SECTR_ToolTip("A very, very loud sound.", null, false)]
	public SECTR_AudioCue ExplosionCue;
	[SECTR_ToolTip("Dynamic audio prefab to spawn.")]
	public GameObject AudioPrefab;
	[SECTR_ToolTip("Speed at which to throw prefab.")]
	public float PrefabThrowSpeed = 8;

	protected override void OnEnable()
	{
		AddButton(KeyCode.H, "Hide Audio HUD", "Show Audio HUD", ShowHUD);

		if(SoftCue != null)
		{
			AddButton(KeyCode.Alpha1, "Play Drop", null, PlaySoft);
		}
		if(ChatterCue != null)
		{
			AddButton(KeyCode.Alpha2, "Stop Chatter", "Play Chatter", PlayChatter);
		}
		if(GunshotCue != null)
		{
			AddButton(KeyCode.Alpha3, "Play Gunshot", null, PlayGunshot);
		}
		if(ExplosionCue != null)
		{
			AddButton(KeyCode.Alpha4, "Play Explosion", null, PlayExplosion);
		}
		if(AudioPrefab != null)
		{
			AddButton(KeyCode.T, "Throw Audio Cube", null, ThrowPrefab);
		}

		base.OnEnable();
	}
	#endregion

	#region Private Methods
	protected void ShowHUD(bool active)
	{
		SECTR_AudioSystem.AudioSystem.ShowAudioHUD = active;
	}

	protected void PlaySoft(bool active)
	{
		SECTR_AudioSystem.Play(SoftCue, transform.position, false);
	}

	protected void PlayChatter(bool active)
	{
		if(active && !chatterInstance)
		{
			chatterInstance = SECTR_AudioSystem.Play(ChatterCue, transform.position, false);
		}
		else if(!active && chatterInstance)
		{
			chatterInstance.Stop(false);
		}
	}

	protected void PlayGunshot(bool active)
	{
		SECTR_AudioSystem.Play(GunshotCue, transform.position, false);
	}

	protected void PlayExplosion(bool active)
	{
		SECTR_AudioSystem.Play(ExplosionCue, transform.position, false);
	}

	protected void ThrowPrefab(bool active)
	{
		GameObject newObject = (GameObject)GameObject.Instantiate(AudioPrefab, transform.position + transform.forward * 2, transform.rotation);
		if(newObject.GetComponent<Rigidbody>())
		{
			newObject.GetComponent<Rigidbody>().velocity = transform.forward * PrefabThrowSpeed;
		}
	}
	#endregion
}
