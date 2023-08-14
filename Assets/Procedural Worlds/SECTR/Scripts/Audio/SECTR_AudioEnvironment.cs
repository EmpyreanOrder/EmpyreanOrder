using UnityEngine;
using System.Collections;

/// \ingroup Audio
/// An abstract base class for spatial components that add and remove
/// SECTR_AudioAmbience objects from the main SECTR_AudioSystem.
/// 
/// AudioEnvironments interact directly with the AudioSystem's stack of active
/// Ambiences. When the Audio Environment is activated, its AudioAmbience is pushed
/// onto the SECTR_AudioSystem's stack of active Audio Environments, but when
/// the player leaves, the Audio Environment is removed from the
/// stack, wherever it is. This allows Audio Environments
/// to overlap and even be nested within one another.
public abstract class SECTR_AudioEnvironment : MonoBehaviour 
{
	#region Private Details
	private bool ambienceActive = false;
	#endregion

	#region Public Interface
	[SECTR_ToolTip("The configuraiton of the ambient audio in this Reverb Zone.")]
	public SECTR_AudioAmbience Ambience = new SECTR_AudioAmbience();

	/// Returns true if this AudioEnvironment has put its Ambience on the stack.
	public bool Active			{ get { return ambienceActive; } }
	#endregion

	#region Unity Interface
	void OnDisable()
	{
		Deactivate();
	}
	#endregion

	#region Audio Environment Interface
	protected void Activate()
	{
		if(!ambienceActive && enabled)
		{
			SECTR_AudioSystem.PushAmbience(Ambience);
			ambienceActive = true;
		}
	}

	protected void Deactivate()
	{
		if(ambienceActive)
		{
			SECTR_AudioSystem.RemoveAmbience(Ambience);
			ambienceActive = false;
		}
	}
	#endregion
}
