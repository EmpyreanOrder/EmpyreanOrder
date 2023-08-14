// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// \ingroup Vis
/// An Occluder represents a visual obstruction. It will hide any objects behind it
/// (from the perspective of the current SECTR_Culler).
///
/// Occluders are a useful tool for optimizing culling, especially in outdoor scenes
/// where Portals may be rare. Occluders are somewhat expensive, and should be used
/// judiciously, ideally on very large objects which obstruct many objects behind them,
/// not on many small objects.
/// 
/// Like Portals, Occluders are required to be planar, convex shapes. This constraint is
/// satisfactory for many cases, however, it is often desirable that an occluder represent an
/// object with a 3D volume, obstructing regardless of viewing angle. To efficiently allow this
/// behavior, Occluders support an AutoOrient feature, that will automatically orient them
/// towards the current SectorCuller during culling.
[RequireComponent(typeof(SECTR_Member))]
[AddComponentMenu("Procedural Worlds/SECTR/Vis/SECTR Occluder")]
public class SECTR_Occluder : SECTR_Hull 
{
	#region Private Details	
	private SECTR_Member cachedMember;
	// List of all sectors to whom we've added a reference.
	private List<SECTR_Sector> currentSectors = new List<SECTR_Sector>(4);
	
	// Quick access cache for all SectorOccluders in the world.
	private static List<SECTR_Occluder> allOccluders = new List<SECTR_Occluder>(32);
	private static Dictionary<SECTR_Sector, List<SECTR_Occluder>> occluderTable = new Dictionary<SECTR_Sector, List<SECTR_Occluder>>(32);

	#if UNITY_EDITOR
	// Debug rendering stuff
	private Camera cullingCamera = null;
	private static Color ActiveOccluderColor = Color.red;
	private static Color InactiveOccluderColor = Color.gray;
	#endif
	#endregion

	#region Public Interface
	/// Possible axes for auto-orientation.
	public enum OrientationAxis
	{
		/// No auto orientation.
		None,			
		/// Orient all axes.
		XYZ,			
		/// Orient on world space XZ axes.
		XZ,				
		/// Orient on world space XY axes.
		XY,				
		/// Orient on world space YZ axes.
		YZ,				
	};
	
	[SECTR_ToolTip("The axes that should orient towards the camera during culling (if any).")]
	public OrientationAxis AutoOrient = OrientationAxis.None;

	/// Accessor for quickly retrieving all SectorOccluders.
	public static List<SECTR_Occluder> All
	{
		get { return allOccluders; }
	}

	public static List<SECTR_Occluder> GetOccludersInSector(SECTR_Sector sector)
	{
		List<SECTR_Occluder> occluders = null;
		occluderTable.TryGetValue(sector, out occluders);
		return occluders;
	}

	/// Fast access to the required SectorMember sibling.
	public SECTR_Member Member
	{
		get { return cachedMember; }
	}

	public Vector3 MeshNormal
	{
		get { ComputeVerts(); return meshNormal; }
	}

	/// Returns the local to world matrix to be used to transform verts during culling.
	public Matrix4x4 GetCullingMatrix(Vector3 cameraPos)
	{
		if(AutoOrient == OrientationAxis.None)
		{
			return transform.localToWorldMatrix;
		}
		else
		{
			ComputeVerts();
			Vector3 occluderPos = transform.position;
			Vector3 cameraVec = cameraPos - occluderPos;
			switch(AutoOrient)
			{
			case OrientationAxis.XY:
				cameraVec.z = 0f;
				break;
			case OrientationAxis.XZ:
				cameraVec.y = 0f;
				break;
			case OrientationAxis.YZ:
				cameraVec.x = 0f;
				break;
			}

			return Matrix4x4.TRS(occluderPos, Quaternion.FromToRotation(meshNormal, cameraVec), transform.lossyScale);
		}
	}

	#if UNITY_EDITOR
	public Camera CullingCamera
	{
		set { cullingCamera = value; }
	}
	#endif
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		cachedMember = GetComponent<SECTR_Member>();
		cachedMember.Changed += new SECTR_Member.MembershipChanged(_MembershipChanged);
		allOccluders.Add(this);
	}

	void OnDisable()
	{
		allOccluders.Remove(this);
		cachedMember.Changed -= new SECTR_Member.MembershipChanged(_MembershipChanged);
		cachedMember = null;
	}

	#if UNITY_EDITOR
	protected override void OnDrawGizmos()
	{
		base.OnDrawGizmos();
		if(cullingCamera)
		{
			Gizmos.matrix = GetCullingMatrix(cullingCamera.transform.position);
		}
		DrawHull(enabled ? ActiveOccluderColor : InactiveOccluderColor);
		DrawNormal(enabled ? ActiveOccluderColor : InactiveOccluderColor, false);
	}
	#endif
	#endregion

	#region Private Methods
	private void _MembershipChanged(List<SECTR_Sector> left, List<SECTR_Sector> joined)
	{
		// Add ref to all of the new objects first so that we don't unload and then immeditately load again.
		if(joined != null)
		{
			int numJoined = joined.Count;
			for(int sectorIndex = 0; sectorIndex < numJoined; ++sectorIndex)
			{
				SECTR_Sector sector = joined[sectorIndex];
				if(sector)
				{
					List<SECTR_Occluder> occluders;
					if(!occluderTable.TryGetValue(sector, out occluders))
					{
						occluders = new List<SECTR_Occluder>(4);
						occluderTable[sector] = occluders;
					}
					occluders.Add(this);
					currentSectors.Add(sector);
				}
			}
		}
		
		// Dec ref any sectors we're no longer in.
		if(left != null)
		{
			int numLeft = left.Count;
			for(int sectorIndex = 0; sectorIndex < numLeft; ++sectorIndex)
			{
				SECTR_Sector sector = left[sectorIndex];
				// We have to be careful about double-removing on shutdown b/c we don't control
				// order of destruction.
				if(sector && currentSectors.Contains(sector))
				{
					List<SECTR_Occluder> occluders;
					if(occluderTable.TryGetValue(sector, out occluders))
					{
						occluders.Remove(this);
					}
					currentSectors.Remove(sector);
				}
			}
		}
	}
	#endregion
}
