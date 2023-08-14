using UnityEngine;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

public class SECTR_SectorUtils : MonoBehaviour
{
	#region Public Interface

	/// <summary>
	/// Returns true if there are sectors in the scene.
	/// </summary>
	/// <returns>True if there are sectors in the scene.</returns>
	public static bool DoHaveSectors()
	{
		return GameObject.FindObjectOfType(typeof(SECTR_Sector)) != null;
	}

	/// <summary>
	/// Move Objects/Spawn-groups into Sectors. If <paramref name="doGlobalParenting"/> is enabled, Objects not contained in a sector get parented globally using GeNa logic.
	/// </summary>
	/// <example>
	/// Examples:
	/// Existing hierarchy in a sector:
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	/// </code>
	/// after calling
	/// <code>SendObjectsIntoSectors(funGameObjects, ancestors = {parent, grandparent});</code>
	/// the result will be
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	///				funGameObject1
	///				funGameObject2
	/// </code>
	/// while calling
	/// <code>SendObjectsIntoSectors(funGameObjects, ancestors = {parent, grandparent}, mergeSpawns = false);</code>
	/// will result in a hierarcy
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	///			parent
	///				funGameObject1
	///				funGameObject2
	/// </code>
	/// </example>
	/// <remarks>
	/// If <see cref="localizeByBounds"/> is <see langword="true"/>, the parent sector of Object(s) 
	/// will be determined by their bounds (extent). This process also considers certain type of lights/audio 
	/// sources and their area of effect. Cross-sector Object(s) (that spread across multiple sectors, or 
	/// otherwise extend outside of one) will be kept in the global space.
	/// If <see cref="localizeByBounds"/> is <see langword="false"/>, the parent sector will be identified by
	/// which sector contains the Object(s) transform.position.
	/// If <see cref="mergeSpawns"/> is <see langword="false"/>, each call to this method will create a new parent
	/// regardless of if a parent with that name already extists.
	/// </remarks>
	/// <param name="parentsUndoList">The list where new parents creation is tracked</param>
	/// <param name="gameObjects">The Game Objects to be sent to their sector.</param>
	/// <param name="parentLocation">Newly created parents trasform.position is going to be set to this.</param>
	/// <param name="localizeBy">Localization method.</param>
	/// <param name="mergeSpawns">If the hierarchy already exist in sector: Should the object(s) be placed into it instead of creating a new parent?</param>
	/// <param name="doGlobalParenting">Should the items get parented even if they are not in any sector or there are no sectors?</param>
	/// <returns>A list of parent objects created by the method.</returns>
	public static void SendObjectsIntoSectors(
		ref List<GameObject> parentsUndoList,
		List<GameObject> gameObjects,
		Vector3 parentLocation,
		SECTR_Constants.ReparentingMode localizeBy = SECTR_Constants.ReparentingMode.Bounds,
		bool mergeSpawns = true,
		bool doGlobalParenting = false)
	{
		SendObjectsIntoSectors(ref parentsUndoList, gameObjects, parentLocation, new string[0], localizeBy, mergeSpawns, doGlobalParenting);
	}

	/// <summary>
	/// Move spawn groups into sectorized parents. If <paramref name="doGlobalParenting"/> is enabled, Objects not contained in a sector get parented globally similar to how GeNa does it.
	/// </summary>
	/// <example>
	/// Examples:
	/// Existing hierarchy in a sector:
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	/// </code>
	/// after calling
	/// <code>SendObjectsIntoSectors(funGameObjects, ancestors = {parent, grandparent});</code>
	/// the result will be
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	///				funGameObject1
	///				funGameObject2
	/// </code>
	/// while calling
	/// <code>SendObjectsIntoSectors(funGameObjects, ancestors = {parent, grandparent}, mergeSpawns = false);</code>
	/// will result in a hierarcy
	/// <code>
	/// SECTOR
	///		grandparent
	///			parent
	///				GameObject1
	///				GameObject2
	///			parent
	///				funGameObject1
	///				funGameObject2
	/// </code>
	/// </example>
	/// <remarks>
	/// If <see cref="localizeByBounds"/> is <see langword="true"/>, the parent sector of Object(s) 
	/// will be determined by their bounds (extent). This process also considers certain type of lights/audio 
	/// sources and their area of effect. Cross-sector Object(s) (that spread across multiple sectors, or 
	/// otherwise extend outside of one) will be kept in the global space.
	/// If <see cref="localizeByBounds"/> is <see langword="false"/>, the parent sector will be identified by
	/// which sector contains the Object(s) transform.position.
	/// If <see cref="mergeSpawns"/> is <see langword="false"/>, each call to this method will create a new parent
	/// regardless of if a parent with that name already extists.
	/// </remarks>
	/// <param name="parentsUndoList">The list where new parents creation is tracked</param>
	/// <param name="gameObjects">The Game Objects to be sent to their sector.</param>
	/// <param name="parentLocation">Newly created parents trasform.position is going to be set to this.</param>
	/// <param name="hierarchy">Hierarchy for the object(s) from the lowest(their parent) to the highest level ancestor.</param>
	/// <param name="localizeBy">Localization method.</param>
	/// <param name="mergeSpawns">If the hierarchy already exist in sector: Should the object(s) be placed into it instead of creating a new parent?</param>
	/// <param name="doGlobalParenting">Should the items get parented even if they are not in any sector or there are no sectors?</param>
	public static void SendObjectsIntoSectors(
		ref List<GameObject> parentsUndoList,
		List<GameObject> gameObjects,
		Vector3 parentLocation, 
		string[] hierarchy,
		SECTR_Constants.ReparentingMode localizeBy = SECTR_Constants.ReparentingMode.Bounds, 
		bool mergeSpawns = true,
		bool doGlobalParenting = false)
	{
		// If no sectors, just reparent them global
		if (!DoHaveSectors())
		{
			if (doGlobalParenting)
			{
				ParentObjectsGlobally(ref parentsUndoList, gameObjects, parentLocation, hierarchy, mergeSpawns);
			}
			return;
		}

		// Create a candidate list from them
		List<SECTR_SectorChildCandidate> sectorChildCandidates = new List<SECTR_SectorChildCandidate>();
		switch (localizeBy)
		{
			case SECTR_Constants.ReparentingMode.Bounds:
				for (int i = 0; i < gameObjects.Count; i++)
				{
					AddObjToCandidateListByBounds(ref sectorChildCandidates, gameObjects[i].transform, hierarchy);
				}
				break;
			case SECTR_Constants.ReparentingMode.Position:
				for (int i = 0; i < gameObjects.Count; i++)
					{
					AddObjToCandidateListByPosition(ref sectorChildCandidates, gameObjects[i].transform, hierarchy);
				}
				break;
			default:
				throw new System.NotImplementedException("Reparenting mode not recognized: " + localizeBy.ToString());
		}

		List<SECTR_Sector> sectorList = GetTopLevelSectors();
		HashSet<Transform> parentedObjects = new HashSet<Transform>();

		// Reparent them by sectors, so non-mergeSpawns can be correctly handled
		for (int i = 0; i < sectorList.Count; i++)
		{
			Transform groupParent = null;
			for (int candidateIx = sectorChildCandidates.Count - 1; candidateIx >= 0; candidateIx--)
			{
				if (sectorChildCandidates[candidateIx].transform != sectorList[i].transform && SECTR_Geometry.BoundsContainsBounds(sectorList[i].TotalBounds, sectorChildCandidates[candidateIx].bounds))
				{
					// If we don't yet have their parent in this sector, get it.
					if (groupParent == null)
					{
						groupParent = GetParent(ref parentsUndoList, sectorList[i].transform, parentLocation, sectorChildCandidates[candidateIx].ancestors, mergeSpawns);
					}

					// Parent each object
					sectorChildCandidates[candidateIx].transform.parent = groupParent;
					// Can't ensure that we would be removing from the end of the list, so that would be slower
					parentedObjects.Add(sectorChildCandidates[candidateIx].transform);
				}
			}
		}

		// If we have some left to be globally parented
		if (doGlobalParenting)
		{
			List<GameObject> globalObjects = new List<GameObject>();

			for (int candidateIx = sectorChildCandidates.Count - 1; candidateIx >= 0; candidateIx--)
			{
				if (!parentedObjects.Contains(sectorChildCandidates[candidateIx].transform))
				{
					globalObjects.Add(sectorChildCandidates[candidateIx].transform.gameObject);
				}
			}

			ParentObjectsGlobally(ref parentsUndoList, globalObjects, parentLocation, hierarchy, mergeSpawns);
		}
	}

	/// <summary>
	/// Adds the object to a list of <see cref="SECTR_SectorChildCandidate"/>s by looking at it's position.
	/// </summary>
	/// <param name="sectorChildCandidates">The list to add the object to.</param>
	/// <param name="objectTransform">The <see cref="Transform"/> of the object.</param>
	public static void AddObjToCandidateListByPosition(ref List<SECTR_SectorChildCandidate> sectorChildCandidates, Transform objectTransform)
	{
		AddObjToCandidateListByPosition(ref sectorChildCandidates, objectTransform, new string[0]);
	}

	/// <summary>
	/// Adds the object to a list of <see cref="SECTR_SectorChildCandidate"/>s by looking at it's position.
	/// </summary>
	/// <param name="sectorChildCandidates">The list to add the object to.</param>
	/// <param name="objectTransform">The <see cref="Transform"/> of the object.</param>
	/// <param name="ancestors">The local(in sector) hierarchy for the object, from the lowest(its parent) 
	/// to the highest level ancestor.</param>
	public static void AddObjToCandidateListByPosition(ref List<SECTR_SectorChildCandidate> sectorChildCandidates, Transform objectTransform, string[] ancestors)
	{
		sectorChildCandidates.Add(
							new SECTR_SectorChildCandidate
							{
								ancestors = new List<string>(ancestors),
								transform = objectTransform,
								bounds = new Bounds(objectTransform.position, Vector3.zero),
							});
	}

	/// <summary>
	/// Adds the object to a list of <see cref="SECTR_SectorChildCandidate"/>s by looking at it's bounds.
	/// </summary>
	/// <remarks>
	/// The parent sector of Object(s) will be determined by their bounds (extent). This process also considers 
	/// certain type of lights/audio sources and their area of effect. Cross-sector Object(s) (that spread across 
	/// multiple sectors, or otherwise extend outside of one) will be kept in the global space.
	/// </remarks>
	/// <param name="sectorChildCandidates">The list to add the object to.</param>
	/// <param name="objectTransform">The <see cref="Transform"/> of the object.</param>
	public static void AddObjToCandidateListByBounds(ref List<SECTR_SectorChildCandidate> sectorChildCandidates, Transform objectTransform)
	{
		AddObjToCandidateListByBounds(ref sectorChildCandidates, objectTransform, new string[0]);
	}

	/// <summary>
	/// Adds the object to a list of <see cref="SECTR_SectorChildCandidate"/>s by looking at it's bounds.
	/// </summary>
	/// <remarks>
	/// The parent sector of Object(s) will be determined by their bounds (extent). This process also considers 
	/// certain type of lights/audio sources and their area of effect. Cross-sector Object(s) (that spread across 
	/// multiple sectors, or otherwise extend outside of one) will be kept in the global space.
	/// </remarks>
	/// <param name="sectorChildCandidates">The list to add the object to.</param>
	/// <param name="objectTransform">The <see cref="Transform"/> of the object.</param>
	/// <param name="ancestors">The local(in sector) hierarchy for the object, from the lowest(its parent) 
	/// to the highest level ancestor.</param>
	public static void AddObjToCandidateListByBounds(ref List<SECTR_SectorChildCandidate> sectorChildCandidates, Transform objectTransform, string[] ancestors)
	{
		Bounds aggregateBounds = new Bounds();
		bool initBounds = false;
		Renderer[] childRenderers = objectTransform.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in childRenderers)
		{
			Bounds renderBounds = renderer.bounds;

			// Particle bounds are unreliable in editor, so use a unit sized box as a proxy.
			if (renderer.GetType() == typeof(ParticleSystemRenderer))
			{
				renderBounds = new Bounds(objectTransform.position, Vector3.one);
			}

			if (!initBounds)
			{
				aggregateBounds = renderBounds;
				initBounds = true;
			}
			else
			{
				aggregateBounds.Encapsulate(renderBounds);
			}
		}
		Light[] childLights = objectTransform.GetComponentsInChildren<Light>();
		foreach (Light light in childLights)
		{
			if (!initBounds)
			{
				aggregateBounds = SECTR_Geometry.ComputeBounds(light);
				initBounds = true;
			}
			else
			{
				aggregateBounds.Encapsulate(SECTR_Geometry.ComputeBounds(light));
			}
		}
		if (initBounds)
		{
			sectorChildCandidates.Add(
				new SECTR_SectorChildCandidate
				{
					ancestors = new List<string>(ancestors),
					transform = objectTransform,
					bounds = aggregateBounds
				});
		};
	}

	public static List<SECTR_Sector> GetTopLevelSectors()
	{
		List<SECTR_Sector> topLevelSectors = new List<SECTR_Sector>();
		SECTR_Sector[] sectors = (SECTR_Sector[])GameObject.FindObjectsOfType(typeof(SECTR_Sector));

		foreach (SECTR_Sector sector in sectors)
		{
			// Check if it has a parent sector
			bool topLevelSector = true;
			Transform parent = sector.transform.parent;
			while (parent != null)
			{
				if (parent.GetComponent<SECTR_Sector>() != null)
				{
					topLevelSector = false;
					break;
				}
				parent = parent.parent;
			}

			if (topLevelSector)
			{
				topLevelSectors.Add(sector);
			}
		}

		return topLevelSectors;
	}

	/// <summary>
	/// Encapsulate a list of <see cref="SECTR_SectorChildCandidate"/>s into sectors according to their settings.
	/// </summary>
	/// <remarks>
	/// Finds all top level sectors (in case there are some nested sectors) and encapsulate the list of 
	/// <see cref="SECTR_SectorChildCandidate"/>s in them.
	/// </remarks>
	/// <param name="sectorChildCandidates">The list of <see cref="SECTR_SectorChildCandidate"/>s.</param>
	/// <param name="undoString">The undo string to use when reparenting.</param>
	public static void Encapsulate(List<SECTR_SectorChildCandidate> sectorChildCandidates, string undoString)
	{
		var topLevelSectors = GetTopLevelSectors();
		for (int i = 0; i < topLevelSectors.Count; i++)
		{
			Encapsulate(topLevelSectors[i], sectorChildCandidates, undoString);
		}
	}

	/// <summary>
	/// Look through the list of <see cref="SECTR_SectorChildCandidate"/>s and reparent those which are in <paramref name="newSector"/> according to their settings.
	/// </summary>
	/// <param name="newSector">The sector to check which objects it contains.</param>
	/// <param name="sectorChildCandidates">The list of <see cref="SECTR_SectorChildCandidate"/>s.</param>
	/// <param name="undoString">The undo string to use when reparenting.</param>
	public static void Encapsulate(SECTR_Sector newSector, List<SECTR_SectorChildCandidate> sectorChildCandidates, string undoString)
	{
		for (int i = 0; i < sectorChildCandidates.Count; i++)
		{
			if (sectorChildCandidates[i].transform != newSector.transform && SECTR_Geometry.BoundsContainsBounds(newSector.TotalBounds, sectorChildCandidates[i].bounds))
			{
				Transform parent = newSector.transform;

				// Create and/or walk the desired hierarchy (if any)
				if (sectorChildCandidates[i].ancestors != null && sectorChildCandidates[i].ancestors.Count > 0)
				{
					for (int ancestorIx = sectorChildCandidates[i].ancestors.Count - 1; ancestorIx >= 1; ancestorIx--)
					{
						bool ancestorExist = false;
						for (int childIx = 0; childIx < parent.childCount; childIx++)
						{
							// If exist, select it
							if (parent.GetChild(childIx).name == sectorChildCandidates[i].ancestors[ancestorIx])
							{
								parent = parent.GetChild(childIx);
								ancestorExist = true;
								break;
							}
						}

						// If it doesn't exist, create and select it
						if (!ancestorExist)
						{
							parent = UndoParent(parent, new GameObject(sectorChildCandidates[i].ancestors[ancestorIx]).transform, undoString);
						}
					}
				}

				// Parent the object itself
				UndoParent(parent, sectorChildCandidates[i].transform, undoString);
			}
		}
	}

	/// <summary>
	/// Assigns parent Transform to a child Transform with Undo and returns the child.
	/// </summary>
	/// <param name="parent">Parent Transform</param>
	/// <param name="child">Child Transform</param>
	/// <param name="undoString">String to appear in the Undo list.</param>
	/// <returns>Child Transform</returns>
	public static Transform UndoParent(Transform parent, Transform child, string undoString)
	{
#if UNITY_EDITOR
		Undo.SetTransformParent(child, parent, undoString);
#else
		child.transform.parent = parent.transform;
#endif
		return child;
	}

	/// <summary>
	/// Assigns parent Transform to a child Transform with Undo and returns the child.
	/// </summary>
	/// <param name="parent">Parent GameObject</param>
	/// <param name="child">Child GameObject</param>
	/// <param name="undoString">String to appear in the Undo list.</param>
	public static void UndoParent(GameObject parent, GameObject child, string undoString)
	{
#if UNITY_EDITOR
		Undo.SetTransformParent(child.transform, parent.transform, undoString);
#else
		child.transform.parent = parent.transform;
#endif

	}

#if UNITY_EDITOR
    /// <summary>
    /// Return the Sectr directory in the project
    /// </summary>
    /// <returns>If in editor it returns the full cts directory, if in build, returns assets directory.</returns>
    public static string GetSectrDirectory()
    {
        string[] assets = AssetDatabase.FindAssets("Sectr_ReadMe", null);
        for (int idx = 0; idx < assets.Length; idx++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assets[idx]);
            if (Path.GetFileName(path) == "Sectr_ReadMe.txt")
            {
                return Path.GetDirectoryName(path) + "/";
            }
        }
        return "";
    }
#endif

    public static double GetUnixTimeStamp()
    {
        return (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
    }
    #endregion

    #region Private helper methods

    private static Transform GetParent(ref List<GameObject> newParentList, Transform parentOfHierarchy, Vector3 parentLocation, List<string> hierarchy, bool mergeSpawns)
	{
		Transform parent = parentOfHierarchy;

		// Create and/or walk the desired hierarchy (if any)
		if (hierarchy != null && hierarchy.Count > 0)
		{
			Transform newParent = null;
			// Start at the top level
			for (int i = hierarchy.Count - 1; i >= 0; i--)
			{
				bool createNew = true;
				for (int childIx = 0; childIx < parent.childCount; childIx++)
				{
					// If exist, select it
					if (parent.GetChild(childIx).name == hierarchy[i])
					{
						// Unless mergeSpawns is false AND we are at the parent(bottom) level
						if (mergeSpawns == false && i == 0)
						{
							break;
						}
						parent = parent.GetChild(childIx);
						createNew = false;
						break;
					}
				}

				// If it doesn't exist, create and select it
				if (createNew)
				{
					newParent = new GameObject(hierarchy[i]).transform;
					newParent.position = parentLocation;
					newParent.parent = parent;
					parent = newParent;
				}
			}

			// The last created is the actual parent (if any)
			if (newParent != null)
			{
				newParentList.Add(newParent.gameObject);
			}
		}

		return parent;
	}

	private static GameObject GetGlobalParent(ref List<GameObject> newParents, Vector3 parentLocation, string[] hierarchy, bool mergeSpawns)
	{
		GameObject globalParent = null;

		// See if any object with the name of the top level in hierarchy already exist at root level
		string topLevelAncestorName = hierarchy[hierarchy.Length - 1];
		foreach (var go in FindObjectsOfType(typeof(GameObject)) as GameObject[])
		{
			if (go.transform.parent == null && go.name == topLevelAncestorName)
			{
				globalParent = go;
				break;
			}
		}

		// If it's just the parent itself in the hierarchy
		if (hierarchy.Length == 1)
		{
			if (globalParent == null || mergeSpawns == false)
			{
				globalParent = new GameObject(topLevelAncestorName);
				globalParent.transform.position = parentLocation;
				newParents.Add(globalParent);
			}
		}
		else
		{
			List<string> hierarchyBelowTopLvl = new List<string>(hierarchy);
			hierarchyBelowTopLvl.RemoveAt(hierarchyBelowTopLvl.Count - 1);

			if (globalParent == null)
			{
				globalParent = new GameObject(topLevelAncestorName);
				globalParent.transform.position = parentLocation;
			}

			globalParent = GetParent(ref newParents, globalParent.transform, parentLocation, hierarchyBelowTopLvl, mergeSpawns).gameObject;
		}

		return globalParent;
	}

	/// <summary>
	/// Parent objects at the root level.
	/// </summary>
	/// <param name="newParents">The list where new parents creation is tracked</param>
	/// <param name="gameObjects">A list of Game Objects.</param>
	/// <param name="parentLocation">Desired position for the parent Game Object.</param>
	/// <param name="hierarchy">Hierarchy for the object(s) from the lowest(their parent) to the highest level ancestor at the root level.</param>
	/// <param name="mergeSpawns">If the root level hierarchy already exist: Should the object(s) be placed into it instead of creating a new parent?</param>
	/// <returns>A list of parent objects created by the method.</returns>
	private static void ParentObjectsGlobally(ref List<GameObject> newParents, List<GameObject> gameObjects, Vector3 parentLocation, string[] hierarchy, bool mergeSpawns)
	{
		if (gameObjects.Count < 1 || hierarchy.Length < 1)
		{
			// Nothing to see here
			return;
		}

		GameObject globalParent = GetGlobalParent(ref newParents, parentLocation, hierarchy, mergeSpawns);

		//Now add the objects
		for (int idx = 0; idx < gameObjects.Count; idx++)
		{
			gameObjects[idx].transform.parent = globalParent.transform;
		}
	}

#endregion
}
