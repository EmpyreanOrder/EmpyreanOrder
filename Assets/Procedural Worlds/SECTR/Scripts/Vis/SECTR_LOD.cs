// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

/// \ingroup Vis
/// Implements a simple Level of Detail (LOD) system for SECTR objects.
///
/// LOD in SECTR is based on the size of the object bounds in screen space,
/// which is the same metric as the LOD system in Unity Pro. SECTR LODs may
/// have as many LOD objects as desired, and can affect any game object, not
/// just renderers. LOD requires a SECTR_CullingCamera in the scene for the LODs
/// to be updated.
[ExecuteInEditMode]
[RequireComponent(typeof(SECTR_Member))]
[AddComponentMenu("Procedural Worlds/SECTR/Vis/SECTR LOD")]
public class SECTR_LOD : MonoBehaviour 
{
	#region Private Details
	[SerializeField] [HideInInspector] private Vector3 boundsOffset;
	[SerializeField] [HideInInspector] private float boundsRadius;
	[SerializeField] [HideInInspector] private bool boundsUpdated;
	private int activeLOD = 0;
	private bool siblingsDisabled = false;
	private SECTR_Member cachedMember = null;
	private List<GameObject> toHide = new List<GameObject>(32);
	private List<LODEntry> toShow = new List<LODEntry>(32);

	private static List<SECTR_LOD> allLODs = new List<SECTR_LOD>(128);
	#endregion

	#region Public Interface
	[System.Serializable]
	public class LODEntry
	{
		public GameObject gameObject;
		public Renderer lightmapSource;
	}

	[System.Serializable]
	public class LODSet
	{
		[SerializeField] private List<LODEntry> lodEntries = new List<LODEntry>(16);
		[SerializeField] private float threshold;

		public List<LODEntry> LODEntries 
		{
			get { return lodEntries; }
		}

		public float Threshold 
		{
			get { return threshold; }
			set { threshold = value; }
		}

		public LODEntry Add(GameObject gameObject, Renderer lightmapSource)
		{
			if(GetEntry(gameObject) == null)
			{
				SECTR_LOD.LODEntry newEntry = new SECTR_LOD.LODEntry();
				newEntry.gameObject = gameObject;
				newEntry.lightmapSource = lightmapSource;
				lodEntries.Add(newEntry);
				return newEntry;
			}
			else
			{
				return null;
			}
		}

		public void Remove(GameObject gameObject)
		{
			int entryIndex = 0;
			while(entryIndex < lodEntries.Count)
			{
				if(lodEntries[entryIndex].gameObject == gameObject)
				{
					lodEntries.RemoveAt(entryIndex);
				}
				else
				{
					++entryIndex;
				}
			}
		}

		public LODEntry GetEntry(GameObject gameObject)
		{
			int numEntries = lodEntries.Count;
			for(int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
			{
				LODEntry entry = lodEntries[entryIndex];
				if(entry.gameObject == gameObject)
				{
					return entry;
				}
			}
			return null;
		}
	};

	/// The set of bitflags that determine which siblings, if any
	/// are disabled when the LOD is culled.
	[System.Flags]
	public enum SiblinglFlags
	{
		/// Disable sibling behaviors that are not this component or the member.
		Behaviors 	= 1 << 0,				
		/// Disable sibling members
		Renderers 	= 1 << 1,				
		/// Disable sibling lights
		Lights 		= 1 << 2,	
		Colliders	= 1 << 3,
		RigidBodies = 1 << 4,
	};

	/// Accessor for global list of active SECTR_LOD components.
	public static List<SECTR_LOD> All
	{
		get { return allLODs; }
	}

	/// This list of LOD sets for this object.
	public List<LODSet> LODs = new List<LODSet>();

	/// When set to true disables sibling mono behaviors,
	/// renderers and lights when the system is culled.
	[SECTR_ToolTip("Determines which sibling components are disabled when the LOD is culled.", null, typeof(SiblinglFlags))]
	public SiblinglFlags CullSiblings = 0;

	/// Picks the correct LOD based on the specified camera.
	/// <param name="renderCamera">The camera for which to select the LOD.</param>
	public void SelectLOD(Camera renderCamera)
	{
		if(renderCamera)
		{
			if(!boundsUpdated)
			{
				_CalculateBounds();
			}

			Vector3 boundsCenter = transform.localToWorldMatrix.MultiplyPoint3x4(boundsOffset);
			float distanceToCamera = Vector3.Distance(renderCamera.transform.position, boundsCenter);
			float screenPercentage = (boundsRadius / (Mathf.Tan(renderCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distanceToCamera)) * 2f;

			int bestLOD = -1;
			int numLODs = LODs.Count;
			for(int lodIndex = 0; lodIndex < numLODs; ++lodIndex)
			{
				float threshold = LODs[lodIndex].Threshold;
				if(lodIndex == activeLOD)
				{
					threshold -= threshold * 0.1f;
				}
				if(screenPercentage >= threshold)
				{
					bestLOD = lodIndex;
					break;
				}
			}

			if(bestLOD != activeLOD)
			{
				_ActivateLOD(bestLOD);
			}
		}
	}

#if UNITY_EDITOR
	/// Resets the LOD 
	public void Reset()
	{
		if(cachedMember && !EditorApplication.isPlaying)
		{
			_ActivateLOD(-1);
			boundsUpdated = false;
			_ActivateLOD(0);
		}
	}
#endif
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		allLODs.Add(this);
		cachedMember = GetComponent<SECTR_Member>();
		SECTR_CullingCamera cullingCamera = SECTR_CullingCamera.All.Count > 0 ? SECTR_CullingCamera.All[0] : null;
		if(cullingCamera
#if UNITY_EDITOR
		   && EditorApplication.isPlaying
#endif
		   )
		{
			SelectLOD(cullingCamera.GetComponent<Camera>());
		}
		else
		{
			_ActivateLOD(0);
		}

	}

	void OnDisable()
	{
		allLODs.Remove(this);
		cachedMember = null;
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.matrix = Matrix4x4.identity;
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.localToWorldMatrix.MultiplyPoint(boundsOffset), boundsRadius);
	}
	#endregion

	#region Private Interface
	void _ActivateLOD(int lodIndex)
	{
		toHide.Clear();
		toShow.Clear();
		
		if(activeLOD >= 0 && activeLOD < LODs.Count)
		{
			LODSet set = LODs[activeLOD];
			int numEntries = set.LODEntries.Count;
			for(int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
			{
				LODEntry entry = set.LODEntries[entryIndex];
				if(entry.gameObject)
				{
					toHide.Add(entry.gameObject);
				}
			}
		}
		
		if(lodIndex >= 0 && lodIndex < LODs.Count)
		{
			LODSet set = LODs[lodIndex];
			int numEntries = set.LODEntries.Count;
			for(int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
			{
				LODEntry entry = set.LODEntries[entryIndex];
				if(entry.gameObject)
				{
					toHide.Remove(entry.gameObject);
					toShow.Add(entry);
				}
			}
		}
		
		int numHidden = toHide.Count;
		for(int hiddenIndex = 0; hiddenIndex < numHidden; ++hiddenIndex)
		{
			toHide[hiddenIndex].SetActive(false);
		}
		
		int numShown = toShow.Count;
		for(int showIndex = 0; showIndex < numShown; ++showIndex)
		{
			LODEntry entry = toShow[showIndex];
			entry.gameObject.SetActive(true);
			if(entry.lightmapSource)
			{
				Renderer entryRenderer = entry.gameObject.GetComponent<Renderer>();
				if(entryRenderer)
				{
					entryRenderer.lightmapIndex = entry.lightmapSource.lightmapIndex;
					entryRenderer.lightmapScaleOffset = entry.lightmapSource.lightmapScaleOffset;
				}
			}
		}

		activeLOD = lodIndex;

		if(CullSiblings != 0 && 
		   ((activeLOD == -1 && !siblingsDisabled) || (activeLOD != -1 && siblingsDisabled)))
		{
			siblingsDisabled = (activeLOD == -1);

			if((CullSiblings & SiblinglFlags.Behaviors) != 0)
			{
				MonoBehaviour[] siblingBehaviors = gameObject.GetComponents<MonoBehaviour>();
				int numBehaviors = siblingBehaviors.Length;
				for(int behaviorIndex = 0; behaviorIndex < numBehaviors; ++behaviorIndex)
				{
					MonoBehaviour behavior = siblingBehaviors[behaviorIndex];
					if(behavior != this && behavior != cachedMember)
					{
						behavior.enabled = !siblingsDisabled;
					}
				}
			}

			if((CullSiblings & SiblinglFlags.Renderers) != 0)
			{
				Renderer[] siblingRenderers = gameObject.GetComponents<Renderer>();
				int numRenderers = siblingRenderers.Length;
				for(int rendererIndex = 0; rendererIndex < numRenderers; ++rendererIndex)
				{
					siblingRenderers[rendererIndex].enabled = !siblingsDisabled;
				}
			}

			if((CullSiblings & SiblinglFlags.Lights) != 0)
			{
				Light[] siblingLights = gameObject.GetComponents<Light>();
				int numLights = siblingLights.Length;
				for(int lightIndex = 0; lightIndex < numLights; ++lightIndex)
				{
					siblingLights[lightIndex].enabled = !siblingsDisabled;
				}
			}

			if((CullSiblings & SiblinglFlags.Colliders) != 0)
			{
				Collider[] siblingColliders = gameObject.GetComponents<Collider>();
				int numColliders = siblingColliders.Length;
				for(int colliderIndex = 0; colliderIndex < numColliders; ++colliderIndex)
				{
					siblingColliders[colliderIndex].enabled = !siblingsDisabled;
				}
			}

			if((CullSiblings & SiblinglFlags.RigidBodies) != 0)
			{
				Rigidbody[] siblingRBs = gameObject.GetComponents<Rigidbody>();
				int numRBs = siblingRBs.Length;
				for(int rbIndex = 0; rbIndex < numRBs; ++rbIndex)
				{
					if(siblingsDisabled)
					{
						siblingRBs[rbIndex].Sleep();
					}
					else
					{
						siblingRBs[rbIndex].WakeUp();
					}
				}
			}
		}
		
		cachedMember.ForceUpdate(true);
	}

	private void _CalculateBounds()
	{
		Bounds bounds = new Bounds();
		int numLODs = LODs.Count;
		bool boundsInitialized = false;
		for(int LODindex = 0; LODindex < numLODs; ++LODindex)
		{
			LODSet lodSet = LODs[LODindex];
			int numObjects = lodSet.LODEntries.Count;
			for(int objectIndex = 0; objectIndex < numObjects; ++objectIndex)
			{
				GameObject lodObject = lodSet.LODEntries[objectIndex].gameObject;
				Renderer lodRenderer = lodObject ? lodObject.GetComponent<Renderer>() : null;
				if(lodRenderer && lodRenderer.bounds.extents != Vector3.zero)
				{
					if(!boundsInitialized)
					{
						bounds = lodRenderer.bounds;
						boundsInitialized = true;
					}
					else
					{
						bounds.Encapsulate(lodRenderer.bounds);
					}
				}
			}
		}
		boundsOffset = transform.worldToLocalMatrix.MultiplyPoint(bounds.center);
		boundsRadius = bounds.extents.magnitude;
		boundsUpdated = true;
	}
	#endregion
}
