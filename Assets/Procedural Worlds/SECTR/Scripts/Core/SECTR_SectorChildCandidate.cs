using UnityEngine;
using System.Collections.Generic;

public struct SECTR_SectorChildCandidate
{
	/// <summary>
	/// Ancestors of the object, from the lowest(its parent) to the highest level ancestor 
	/// that's desired to be reconstucted at the objects new location in the hierarchy.
	/// </summary>
	public List<string> ancestors;

	/// <summary>
	/// Transform of the object.
	/// </summary>
	public Transform transform;

	/// <summary>
	/// Bounds of the object.
	/// </summary>
	public Bounds bounds;
}
