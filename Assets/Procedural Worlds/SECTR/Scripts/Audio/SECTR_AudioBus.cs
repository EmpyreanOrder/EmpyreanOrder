// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

/// \ingroup Audio
/// Represents the configuration of a particular mixing bus, 
/// which can be used to bulk mix SECTR_AudioCue instances. 
/// 
/// Mixing buses are stored in a hierarchy, where the settings cascade down the hierarchy
/// (i.e. if a bus is muted, then so are all of its children). This hierarchical relationship 
/// makes it easier to mix the game than if the volumes of every cue needed to be adjusted
/// individually. Buses can also be used at runtime to do things as simple as providing user
/// controlled FX/Music/Voice sliders, to completely dynamic mixing.
public class SECTR_AudioBus : ScriptableObject
{
	#region Private Details
	[SerializeField] [HideInInspector] private SECTR_AudioBus parent;

	private List<SECTR_AudioBus> children = new List<SECTR_AudioBus>();	
	private float userVolume = 1;
	private float userPitch = 1;
	private float effectiveVolume = 1;
	private float effectivePitch = 1;
	private bool muted = false;

#if UNITY_EDITOR
	private List<SECTR_AudioCue> audioCues = new List<SECTR_AudioCue>();
#endif
	#endregion

	#region Public Interface
	[SECTR_ToolTip("The volume of this bus, between 0 and 1.", 0f, 1f)]
	public float Volume = 1;
	[SECTR_ToolTip("The pitch of this bus, between 0 and 2.", 0f, 2f)]
	public float Pitch = 1;

	/// Accessor for the user volume. This is a volume that is not saved
	/// and is applied on top of the volume set in the original resource.
	public float UserVolume
	{
		set { userVolume = value; }
		get { return userVolume; }
	}

	public float UserPitch
	{
		set { userPitch = value; }
		get { return userPitch; }
	}

	/// (Un)Mutes this bus, and all of the sounds in it.
	public bool Muted
	{
		get { return muted; }
		set { muted = value; }
	}

	/// An optimization that returns the current, flattened bus volume.
	public float EffectiveVolume
	{
		get { return effectiveVolume; }
		set { effectiveVolume = muted ? 0f : Mathf.Clamp01(Volume * userVolume * value); }
	}

	/// An optimization that returns the current, flattened bus pitch.
	public float EffectivePitch
	{
		get { return effectivePitch; }
		set { effectivePitch = Mathf.Clamp(Pitch * userPitch * value, 0f, 2f); }
	}

	/// Accessor for this Bus's parent (if any).
	public SECTR_AudioBus Parent
	{
		set
		{
			if(value != parent && value != this)
			{
				if(parent)
				{
					parent._RemoveChild(this);
				}
				parent = value;
				if(parent)
				{
					parent._AddChild(this);
				}
				#if UNITY_EDITOR
				EditorUtility.SetDirty(this);
				#endif
			}
		}
		get { return parent; }
	}

	/// Returns the list of buses that are children of this bus.
	public List<SECTR_AudioBus> Children
	{
		get { return children; }
	}
	
	/// Determines whether this instance is an ancestor of the specified bus.
	/// <param name="bus">The bus to check ancestry of.</param>
	/// <returns>Returns true if this bus is an ancestor of the specified bus.</returns>
	public bool IsAncestorOf(SECTR_AudioBus bus)
	{
		SECTR_AudioBus decendent = bus;
		while(decendent != null)
		{
			if(decendent == this)
			{
				return true;
			}
			decendent = decendent.Parent;
		}
		return false;
	}

	/// Determines whether this instance is a decendent of the specified bus.
	/// <param name="bus">The bus to check ancestry of.</param>
	/// <returns>Returns true if this bus is a decendent of the specified bus.</returns>
	public bool IsDecendentOf(SECTR_AudioBus bus)
	{
		SECTR_AudioBus ancestor = Parent;
		while(ancestor != null)
		{
			if(ancestor == bus)
			{
				return true;
			}
			ancestor = ancestor.Parent;
		}
		return false;
	}

	/// Resets the User Volume to 1 for this bus and all it's children.
	public void ResetUserVolume()
	{
		userVolume = 1f;
		int numChildren = children.Count;
		for(int childIndex = 0; childIndex < numChildren; ++childIndex)
		{
			SECTR_AudioBus child = children[childIndex];
			if(child)
			{
				child.ResetUserVolume();
			}
		}
	}

#if UNITY_EDITOR
	public void AddCue(SECTR_AudioCue cue)
	{
		if(cue != null && !audioCues.Contains(cue))
		{
			audioCues.Add(cue);
			audioCues.Sort(delegate(SECTR_AudioCue x, SECTR_AudioCue y) 
			{
				if(x == null && y == null)
				{
					return 0;
				}
				if(x == null && y != null)
				{
					return -1;
				}
				if(x != null && y == null)
				{
					return 1;
				}
				else
				{
					return x.name.CompareTo(y.name);
				}
			});
		}
	}

	public void RemoveCue(SECTR_AudioCue cue)
	{
		audioCues.Remove(cue);
	}

	public List<SECTR_AudioCue> Cues
	{
		get { return audioCues; }
	}
#endif
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		if(parent)
		{
			parent._AddChild(this);
		}

#if UNITY_EDITOR
		int childIndex = 0;
		while(childIndex < children.Count)
		{
			if(children[childIndex] == null)
			{
				children.RemoveAt(childIndex);
			}
			else
			{
				++childIndex;
			}
		}
#endif
	}

	void OnDisable()
	{
		if(parent)
		{
			parent._RemoveChild(this);
		}
	}
	#endregion

	#region Private Methods
	private void _AddChild(SECTR_AudioBus child)
	{
		if(child != null && !children.Contains(child))
		{
			children.Add(child);
#if UNITY_EDITOR
            
			children.Sort(delegate(SECTR_AudioBus x, SECTR_AudioBus y) 
			{
                //Can be null during asset import!
                if (x != null && y != null)
                    return x.name.CompareTo(y.name);
                else
                    return 0;
			});
#endif
		}
	}

	private void _RemoveChild(SECTR_AudioBus child)
	{
		children.Remove(child);
	}
	#endregion
}
