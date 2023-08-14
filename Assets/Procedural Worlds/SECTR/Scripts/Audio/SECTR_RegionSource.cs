// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;

/// \ingroup Audio
/// Playes a SECTR_AudioCue within a 3D volume.
/// 
/// It's often desirable to represent a sound not as a single point, but as an entire region of
/// space. RegionSources make that possible by efficiently computing the nearest point on the spline to the active
/// listener, and positioning its sound instance at that location. This creates a very convincing
/// illusing of the sound eminating from the entire spline, while using only one actual audio instance.
/// 
/// RegionSource supports any collider that Unity allows. However, for performance reasons it will default to
/// using the AABB of whatever collider is used. If more accuracy is desired, raycasting can be enabled, which
/// will determine the exact closest point (at some additional CPU cost.
[ExecuteInEditMode]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Region Source")]
public class SECTR_RegionSource : SECTR_PointSource 
{
	#region Public Interface
	[SECTR_ToolTip("Determine the closest point by raycast instead of bounding box. More accurate but more expensive.")]
	public bool Raycast = false;
	#endregion

	#region Unity Interface
	#if UNITY_EDITOR
	void OnEnable()
	{
		EditorApplication.update += Update;
	}
	
	protected override void OnDisable()
	{
		base.OnDisable();	
		EditorApplication.update -= Update;
	}
	#endif

	void Update()
	{
		if(instance)
		{
			Vector3 systemPosition = SECTR_AudioSystem.Listener.position;
			Vector3 closestPoint = transform.position;
			Collider regionCollider = GetComponent<Collider>();
			if(Raycast && regionCollider)
			{
				RaycastHit hit;
				Vector3 vecToCollider = transform.position - systemPosition;
				float distance = vecToCollider.magnitude;
				vecToCollider /= distance;
				if(regionCollider.Raycast(new Ray(systemPosition, vecToCollider), out hit, distance))
				{
					closestPoint = hit.point;
				}
				else
				{
					closestPoint = systemPosition;
				}
			}
			else if(regionCollider)
			{
				if(regionCollider.bounds.Contains(systemPosition))
				{
					closestPoint = systemPosition;
				}
				else
				{
					closestPoint = regionCollider.ClosestPointOnBounds(systemPosition);
				}
			}

			instance.Position = closestPoint;
		}
	}
	#endregion
}
