// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(SECTR_Member))]
/// \ingroup Vis
/// Vestigial component from older version of SECTR. 
/// Left intact only for backwards compatability.
[AddComponentMenu("")]
public class SECTR_Culler : MonoBehaviour
{
	#region Private Members
	private SECTR_Member cachedMember;
	#endregion

	#region Public Interface
	[SECTR_ToolTip("Overrides the culling information on Member.")]
	public bool CullEachChild = false;
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		cachedMember = GetComponent<SECTR_Member>();
		cachedMember.ChildCulling = CullEachChild ? SECTR_Member.ChildCullModes.Individual : SECTR_Member.ChildCullModes.Group;
	}

	void OnDisable()
	{
	}
	#endregion
}
