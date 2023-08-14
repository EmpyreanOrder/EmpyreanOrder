// Copyright © Procedural Worlds Pty Limited.  All Rights Reserved. 
// Copyright (C) Procedural Worlds Pty Limited.  All Rights Reserved. 

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

public class SECTR_TerrainWindow : SECTR_Window
{
	#region Private Details

	private const int TARGET_SPLIT_RESOLUTION = 512;

    private Vector2 m_windowScrollPosition;
    private Vector2 m_listScrollPosition;
	private string m_sectorSearch = "";
	private Terrain m_selectedTerrain = null;

	private int m_sectorsPerWidth = 4;
	private int m_sectorsPerHeight = 1;
	private int m_sectorsPerLength = 4;

	private int m_terrainSplitPower = 2;
	private int m_maxTerrainDivisionPower = 4;
	private GUIContent m_piecesContent = new GUIContent("No terrain selected");
	private GUIContent m_newSize = new GUIContent("No terrain selected");
	private GUIContent m_newHeightRes = new GUIContent("No terrain selected");
	private GUIContent m_newDetailRes = new GUIContent("No terrain selected");
	private GUIContent m_newSplatRes = new GUIContent("No terrain selected");
	private GUIContent m_newBaseRes = new GUIContent("No terrain selected");

	private bool m_sectorizeConnected = false;
	private bool m_splitTerrain = true; // Splitting by default
    private bool m_sortGaiaObjects = true;
    private bool m_createPortalGeo = false; 
	private bool m_groupStaticObjects = false;
	private bool m_groupDynamicObjects = false;

	private SECTR_Constants.ReparentingMode m_dropReparentingMode = SECTR_Constants.ReparentingMode.Bounds;

	private GUIStyle m_sectorCountFieldStyle = null;
	private GUIStyle m_dropAreaStyle = null;
	//private Texture2D m_dropAreaBg;

	private readonly GUIContent SECTOR_COUNT_HEADER_LABEL = new GUIContent("Number of sectors");

	private readonly GUIContent SECTORS_PER_WIDTH_LABEL = new GUIContent("per Width",
		"Number of Sectors to create along the terrain's width.");
	private readonly GUIContent SECTORS_PER_LENGTH_LABEL = new GUIContent("per Length",
		"Number of Sectors to create along the terrain's length.");
	private readonly GUIContent SECTORS_PER_HEIGHT_LABEL = new GUIContent("per Height",
		"Number of Sectors to create along the terrain's height.");

	private readonly GUIContent SECTORIZE_CONNECTED_LABEL = new GUIContent("Include Connected",
		"Sectorizes all terrains directly or indirectly connected to selected terrain.");
	private readonly GUIContent SPLIT_TERRAIN_LABEL = new GUIContent("Split Terrain",
		"Splits terrain into multiple objects (for streaming or culling).");
#if GAIA_PRESENT
    private readonly GUIContent SORT_GAIA_SPAWNS_LABEL = new GUIContent("Sort Gaia GameObjects",
        "Sorts all spawned Gaia GameObjects into the created sectors. Deactivated elements will be skipped.");
#endif
    private readonly GUIContent CREATE_PORTALS_MESH_LABEL = new GUIContent("Create Mesh for Portals",
		"Creates a mesh for the portal for games that need it. Not required.");
	private readonly GUIContent GROUP_STATIC_LABEL = new GUIContent("Group Static Objects",
		"Make all static game objects on the terrain children of the Sector.");
	private readonly GUIContent GROUP_DYNAMIC_LABEL = new GUIContent("Group Dynamic Objects",
		"Make all dynamic game objects on the terrain children of the Sector.");

	private readonly GUIContent SPLIT_POWER_LABEL = new GUIContent("Scale", "Set the required scale.");
	private readonly GUIContent PIECES_LABEL = new GUIContent("Sector count",
		"The number of pieces/sectors to be created by the split.");
	private readonly GUIContent SPLIT_SIZE_LABEL = new GUIContent("Sector size", "Size of each piece/sector.");
	private readonly GUIContent HEIGHT_RESOLUTION_LABEL = new GUIContent("Heightmap",
		"Heightmap resolution of each terrain piece.");
	private readonly GUIContent DETAIL_RESOLUTION_LABEL = new GUIContent("Detail",
		"Detail resolution of each terrain piece.");
	private readonly GUIContent CONTROL_RESOLUTION_LABEL = new GUIContent("Control Texture",
		"Control Texture (splatmap) resolution of each terrain piece.");
	private readonly GUIContent BASE_RESOLUTION_LABEL = new GUIContent("Base Texture",
		"Base Texture resolution of each terrain piece.");

	// Drop Objects to move them into Sectors section
	private readonly GUIContent SECTORIZE_OBJECTS_LABEL = new GUIContent("Move Objects into Sectors",
		"Drop objects into the area to add them to the sector where they are located.");

	private readonly GUIContent REPARENTING_MODE_SWITCH_LABEL = new GUIContent("Localize Objects by their",
		"Choose the method by which the parent sector of objects is determined. If going by bounds, " +
		"cross-sector objects will be kept in the global space.\n\n(For more details see the 'SECTR Core Manual')");
	private readonly GUIContent[] REPARENTING_MODES_LABELS = new GUIContent[] {
		new GUIContent("Bounds",
			"The parent sector of Objects will be determined by their bounds (extent). This process also " +
			"considers certain type of lights and their area of effect. Cross-sector Objects (that spread " +
			"across multiple sectors, or otherwise extend outside of one) will be kept in the global space.\n\n" +
			"(For more details on why you would want this, see the 'SECTR Core Manual')"),
		new GUIContent("Position",
			"The parent sector of Objects will be determined by their transform.position."),
	};

	private readonly GUIContent DROP_AREA_LABEL = new GUIContent("Drop Objects to move them into Sectors",
		"Drop objects here to add them to the sector where they are located.\n\n");
#endregion

#region Public Interface
	public static void SectorizeTerrain(Terrain terrain, int sectorsWidth, int sectorsLength, int sectorsHeight, bool splitTerrain, bool createPortalGeo, bool includeStatic, bool includeDynamic, bool sortGaiaObjects)
	{
		if(!terrain)
		{
			Debug.LogWarning("Cannot sectorize null terrain.");
			return;
		}

		if(terrain.transform.root.GetComponentsInChildren<SECTR_Sector>().Length > 0)
		{
			Debug.LogWarning("Cannot sectorize terrain that is already part of a Sector."); 
		}

		string undoString = "Sectorized " + terrain.name;

		if(sectorsWidth == 1 && sectorsLength == 1)
		{
			SECTR_Sector newSector = terrain.gameObject.AddComponent<SECTR_Sector>();
			SECTR_Undo.Created(newSector, undoString);
			newSector.ForceUpdate(true);
			return;
		}

		if(splitTerrain && (!Mathf.IsPowerOfTwo(sectorsWidth) || !Mathf.IsPowerOfTwo(sectorsLength)))
		{
			Debug.LogWarning("Splitting terrain requires power of two sectors in width and length.");
			splitTerrain = false;
		}
		else if(splitTerrain && sectorsWidth != sectorsLength)
		{
			Debug.LogWarning("Splitting terrain requires same number of sectors in width and length.");
			splitTerrain = false;
		}

		int terrainLayer = terrain.gameObject.layer;
		Vector3 terrainSize = terrain.terrainData.size;
		float sectorWidth = terrainSize.x / sectorsWidth;
		float sectorHeight = terrainSize.y / sectorsHeight;
		float sectorLength = terrainSize.z / sectorsLength;

#if UNITY_2019_3_OR_NEWER
        int heightmapWidth = (terrain.terrainData.heightmapResolution / sectorsWidth);
		int heightmapLength = (terrain.terrainData.heightmapResolution / sectorsLength);
#else
        int heightmapWidth = (terrain.terrainData.heightmapWidth / sectorsWidth);
		int heightmapLength = (terrain.terrainData.heightmapHeight / sectorsLength);
#endif
        int alphaWidth = terrain.terrainData.alphamapWidth / sectorsWidth;
		int alphaLength = terrain.terrainData.alphamapHeight / sectorsLength;
		int detailWidth = terrain.terrainData.detailWidth / sectorsWidth;
		int detailLength = terrain.terrainData.detailHeight / sectorsLength;

		string sceneDir = "";
		string sceneName = "";
		string exportFolder = splitTerrain ? SECTR_Asset.MakeExportFolder("TerrainSplits", false, out sceneDir, out sceneName) : "";

		Transform baseTransform = null;
		if(splitTerrain)
		{
			GameObject baseObject = new GameObject(terrain.name);
			baseTransform = baseObject.transform;
			SECTR_Undo.Created(baseObject, undoString);
		}

		List<SECTR_SectorChildCandidate> sectorChildCandidates = new List<SECTR_SectorChildCandidate>();
		_GetRoots(includeStatic, includeDynamic, sectorChildCandidates);
        if (sortGaiaObjects)
        {
            _GetGaiaSpawns(sectorChildCandidates);
        }

		// Create Sectors
		string progressTitle = "Sectorizing Terrain";
		int progressCounter = 0;
		EditorUtility.DisplayProgressBar(progressTitle, "Preparing", 0);

		SECTR_Sector[,,] newSectors = new SECTR_Sector[sectorsWidth,sectorsLength,sectorsHeight];
		Terrain[,] newTerrains = splitTerrain ? new Terrain[sectorsWidth,sectorsLength] : null;
		for(int widthIndex = 0; widthIndex < sectorsWidth; ++widthIndex)
		{
			for(int lengthIndex = 0; lengthIndex < sectorsLength; ++lengthIndex)
			{
				for(int heightIndex = 0; heightIndex < sectorsHeight; ++heightIndex)
				{
					string newName = terrain.name + " " + widthIndex + "-" + lengthIndex + "-" + heightIndex;

					EditorUtility.DisplayProgressBar(progressTitle, "Creating sector " + newName, progressCounter++ / (float)(sectorsWidth * sectorsLength * sectorsHeight));

					GameObject newSectorObject = new GameObject("SECTR " + newName + " Sector");
					newSectorObject.transform.parent = baseTransform;
					Vector3 sectorCorner = new Vector3(widthIndex * sectorWidth,
						heightIndex * sectorHeight,
						lengthIndex * sectorLength) + terrain.transform.position;
					newSectorObject.transform.position = sectorCorner;
					newSectorObject.isStatic = true;
					SECTR_Sector newSector = newSectorObject.AddComponent<SECTR_Sector>();
					newSector.OverrideBounds = !splitTerrain && (sectorsWidth > 1 || sectorsLength > 1);
					newSector.BoundsOverride = new Bounds(sectorCorner + new Vector3(sectorWidth * 0.5f, sectorHeight * 0.5f, sectorLength * 0.5f),
						new Vector3(sectorWidth, sectorHeight, sectorLength));
					newSectors[widthIndex,lengthIndex,heightIndex] = newSector;

					if(splitTerrain && heightIndex == 0)
					{
						GameObject newTerrainObject = new GameObject(newName + " Terrain");
						newTerrainObject.layer = terrainLayer;
						newTerrainObject.tag = terrain.tag;
						newTerrainObject.transform.parent = newSectorObject.transform;
						newTerrainObject.transform.localPosition = Vector3.zero;
						newTerrainObject.transform.localRotation = Quaternion.identity;
						newTerrainObject.transform.localScale = Vector3.one;
						newTerrainObject.isStatic = true;
						Terrain newTerrain = newTerrainObject.AddComponent<Terrain>();
						newTerrain.terrainData = SECTR_Asset.Create<TerrainData>(exportFolder, newName, new TerrainData());
						EditorUtility.SetDirty(newTerrain.terrainData);
						SECTR_VC.WaitForVC();

						// Copy properties
						// Basic terrain properties
						newTerrain.editorRenderFlags = terrain.editorRenderFlags;
#if UNITY_2019_1_OR_NEWER
                        newTerrain.shadowCastingMode = terrain.shadowCastingMode;
#else
                        newTerrain.castShadows = terrain.castShadows;
#endif
						newTerrain.heightmapMaximumLOD = terrain.heightmapMaximumLOD;
						newTerrain.heightmapPixelError = terrain.heightmapPixelError;
						newTerrain.lightmapIndex = -1; // Can't set lightmap UVs on terrain.
						newTerrain.materialTemplate = terrain.materialTemplate;
						newTerrain.bakeLightProbesForTrees = terrain.bakeLightProbesForTrees;
#if !UNITY_2019_2_OR_NEWER
                        //in 2019.2 these settings have become obsolete and these settings are supposed to be set in the material directly
                        newTerrain.legacyShininess = terrain.legacyShininess;
						newTerrain.legacySpecular = terrain.legacySpecular;
                        //Copy material
                        newTerrain.materialType = terrain.materialType;
#endif
                        
						// Copy geometric data
						int heightmapBaseX = widthIndex * heightmapWidth;
						int heightmapBaseY = lengthIndex * heightmapLength;
						int heightmapWidthX = heightmapWidth + (sectorsWidth > 1 ? 1 : 0);
						int heightmapWidthY = heightmapLength + (sectorsLength > 1 ? 1 : 0);	
						newTerrain.terrainData.heightmapResolution = terrain.terrainData.heightmapResolution / sectorsWidth;
						newTerrain.terrainData.size = new Vector3(sectorWidth, terrainSize.y, sectorLength);
						newTerrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(heightmapBaseX, heightmapBaseY, heightmapWidthX, heightmapWidthY));
#if !UNITY_2019_3_OR_NEWER
                        newTerrain.terrainData.thickness = terrain.terrainData.thickness;
#endif
						
						// Copy alpha maps
						int alphaBaseX = alphaWidth * widthIndex;
						int alphaBaseY = alphaLength * lengthIndex;
#if UNITY_2018_3_OR_NEWER
                        newTerrain.terrainData.terrainLayers = terrain.terrainData.terrainLayers;
#else
                        newTerrain.terrainData.splatPrototypes = terrain.terrainData.splatPrototypes;
#endif
                        newTerrain.basemapDistance = terrain.basemapDistance;
						newTerrain.terrainData.baseMapResolution = terrain.terrainData.baseMapResolution / sectorsWidth;
						newTerrain.terrainData.alphamapResolution = terrain.terrainData.alphamapResolution / sectorsWidth;
						newTerrain.terrainData.SetAlphamaps(0, 0, terrain.terrainData.GetAlphamaps(alphaBaseX, alphaBaseY, alphaWidth, alphaLength));

						// Copy detail info
						newTerrain.detailObjectDensity = terrain.detailObjectDensity;
						newTerrain.detailObjectDistance = terrain.detailObjectDistance;
						newTerrain.terrainData.detailPrototypes = terrain.terrainData.detailPrototypes;
						newTerrain.terrainData.SetDetailResolution(terrain.terrainData.detailResolution / sectorsWidth, 8); // TODO: extract detailResolutionPerPatch
						newTerrain.collectDetailPatches = terrain.collectDetailPatches;

						int detailBaseX = detailWidth * widthIndex;
						int detailBaseY = detailLength * lengthIndex;
						int numLayers = terrain.terrainData.detailPrototypes.Length;
						for(int layer = 0; layer < numLayers; ++layer)
						{
							newTerrain.terrainData.SetDetailLayer(0, 0, layer, terrain.terrainData.GetDetailLayer(detailBaseX, detailBaseY, detailWidth, detailLength, layer)); 
						}

						// Copy grass and trees
						newTerrain.terrainData.wavingGrassAmount = terrain.terrainData.wavingGrassAmount;
						newTerrain.terrainData.wavingGrassSpeed = terrain.terrainData.wavingGrassSpeed;
						newTerrain.terrainData.wavingGrassStrength = terrain.terrainData.wavingGrassStrength;
						newTerrain.terrainData.wavingGrassTint = terrain.terrainData.wavingGrassTint;
						newTerrain.treeBillboardDistance = terrain.treeBillboardDistance;
						newTerrain.treeCrossFadeLength = terrain.treeCrossFadeLength;
						newTerrain.treeDistance = terrain.treeDistance;
						newTerrain.treeMaximumFullLODCount = terrain.treeMaximumFullLODCount;
						newTerrain.terrainData.treePrototypes = terrain.terrainData.treePrototypes;
						newTerrain.terrainData.RefreshPrototypes();

						foreach(TreeInstance treeInstace in terrain.terrainData.treeInstances)
						{
							if(treeInstace.prototypeIndex >= 0 && treeInstace.prototypeIndex < newTerrain.terrainData.treePrototypes.Length &&
								newTerrain.terrainData.treePrototypes[treeInstace.prototypeIndex].prefab)
							{
								Vector3 worldSpaceTreePos = Vector3.Scale(treeInstace.position, terrainSize) + terrain.transform.position;
								if(newSector.BoundsOverride.Contains(worldSpaceTreePos))
								{
									Vector3 localSpaceTreePos = new Vector3((worldSpaceTreePos.x - newTerrain.transform.position.x) / sectorWidth,
										treeInstace.position.y,
										(worldSpaceTreePos.z - newTerrain.transform.position.z) / sectorLength);
									TreeInstance newInstance = treeInstace;
									newInstance.position = localSpaceTreePos;
									newTerrain.AddTreeInstance(newInstance);
								}
							}
						}


						// Force terrain to rebuild
						newTerrain.Flush();

						UnityEditor.EditorUtility.SetDirty(newTerrain.terrainData);
						SECTR_VC.WaitForVC();
						newTerrain.enabled = false;
						newTerrain.enabled = true;

						TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
						if(terrainCollider)
						{
							TerrainCollider newCollider = newTerrainObject.AddComponent<TerrainCollider>();	
							newCollider.sharedMaterial = terrainCollider.sharedMaterial;
							newCollider.terrainData = newTerrain.terrainData;
						}

						newTerrains[widthIndex,lengthIndex] = newTerrain;
						SECTR_Undo.Created(newTerrainObject, undoString);
					}
					newSector.ForceUpdate(true);
					SECTR_Undo.Created(newSectorObject, undoString);

					SECTR_SectorUtils.Encapsulate(newSector, sectorChildCandidates, undoString);
				}
			}
		}

		// Create portals and neighbors
		progressCounter = 0;
		for(int widthIndex = 0; widthIndex < sectorsWidth; ++widthIndex)
		{
			for(int lengthIndex = 0; lengthIndex < sectorsLength; ++lengthIndex)
			{
				for(int heightIndex = 0; heightIndex < sectorsHeight; ++heightIndex)
				{
					EditorUtility.DisplayProgressBar(progressTitle, "Creating portals...", progressCounter++ / (float)(sectorsWidth * sectorsLength * sectorsHeight));

					if(widthIndex < sectorsWidth - 1)
					{
						_CreatePortal(createPortalGeo, newSectors[widthIndex + 1, lengthIndex, heightIndex], newSectors[widthIndex, lengthIndex, heightIndex], baseTransform, undoString);
					}

					if(lengthIndex < sectorsLength - 1)
					{
						_CreatePortal(createPortalGeo, newSectors[widthIndex, lengthIndex + 1, heightIndex], newSectors[widthIndex, lengthIndex, heightIndex], baseTransform, undoString);
					}

					if(heightIndex > 0)						
					{
						_CreatePortal(createPortalGeo, newSectors[widthIndex, lengthIndex, heightIndex], newSectors[widthIndex, lengthIndex, heightIndex - 1], baseTransform, undoString);
					}
				}
			}
		}

		if(splitTerrain)
		{
			Component ctsComponent = null;
			Type ctsType = SECTR_Utils.GetType("CTS.CompleteTerrainShader");
			if (ctsType != null)
			{
				ctsComponent = terrain.gameObject.GetComponent(ctsType);
			}

			progressCounter = 0;
			for(int widthIndex = 0; widthIndex < sectorsWidth; ++widthIndex)
			{
				for(int lengthIndex = 0; lengthIndex < sectorsLength; ++lengthIndex)
				{
					EditorUtility.DisplayProgressBar(progressTitle, "Smoothing split terrain...", progressCounter++ / (float)(sectorsWidth * sectorsLength));

					// Blend together the seams of the alpha maps, which requires
					// going through all of the mip maps of all of the layer textures.
					// We have to blend here rather than when we set the alpha data (above)
					// because Unity computes mips and we need to blend all of the mips.
					Terrain newTerrain = newTerrains[widthIndex, lengthIndex];

					SECTR_Sector terrainSector = newSectors[widthIndex, lengthIndex, 0];
					terrainSector.LeftTerrain = widthIndex > 0 ? newSectors[widthIndex - 1, lengthIndex, 0] : null;
					terrainSector.RightTerrain = widthIndex < sectorsWidth - 1 ? newSectors[widthIndex + 1, lengthIndex, 0] : null;
					terrainSector.BottomTerrain = lengthIndex > 0 ? newSectors[widthIndex, lengthIndex - 1, 0] : null;
					terrainSector.TopTerrain = lengthIndex < sectorsLength - 1 ? newSectors[widthIndex, lengthIndex + 1, 0] : null;
					terrainSector.ConnectTerrainNeighbors();

					// Use reflection trickery to get at the raw texture values.
					System.Reflection.PropertyInfo alphamapProperty = newTerrain.terrainData.GetType().GetProperty("alphamapTextures",
						System.Reflection.BindingFlags.NonPublic | 
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.Instance |
						System.Reflection.BindingFlags.Static);
					// Get the texture we'll write into
					Texture2D[] alphaTextures = (Texture2D[])alphamapProperty.GetValue(newTerrain.terrainData, null);
					int numTextures = alphaTextures.Length;

					// Get the textures we'll read from
					Texture2D[] leftNeighborTextures = terrainSector.LeftTerrain != null ? (Texture2D[])alphamapProperty.GetValue(newTerrains[widthIndex - 1, lengthIndex].terrainData, null) : null;
					Texture2D[] rightNeighborTextures = terrainSector.RightTerrain != null ? (Texture2D[])alphamapProperty.GetValue(newTerrains[widthIndex + 1, lengthIndex].terrainData, null) : null;
					Texture2D[] topNeighborTextures = terrainSector.TopTerrain != null ? (Texture2D[])alphamapProperty.GetValue(newTerrains[widthIndex, lengthIndex + 1].terrainData, null) : null;
					Texture2D[] bottomNeighborTextures = terrainSector.BottomTerrain != null ? (Texture2D[])alphamapProperty.GetValue(newTerrains[widthIndex, lengthIndex - 1].terrainData, null) : null;

					for(int textureIndex = 0; textureIndex < numTextures; ++textureIndex)
					{
						Texture2D alphaTexture = alphaTextures[textureIndex];
						Texture2D leftTexture = leftNeighborTextures != null ? leftNeighborTextures[textureIndex] : null;
						Texture2D rightTexture = rightNeighborTextures != null ? rightNeighborTextures[textureIndex] : null;
						Texture2D topTexture = topNeighborTextures != null ? topNeighborTextures[textureIndex] : null;
						Texture2D bottomTexture = bottomNeighborTextures != null ? bottomNeighborTextures[textureIndex] : null;
						int numMips = alphaTexture.mipmapCount;
						for(int mipIndex = 0; mipIndex < numMips; ++mipIndex)
						{
							Color[] alphaTexels = alphaTexture.GetPixels(mipIndex);
							int width = (int)Mathf.Sqrt(alphaTexels.Length);
							int height = width;
							for(int texelWidthIndex = 0; texelWidthIndex < width; ++texelWidthIndex)
							{
								for(int texelHeightIndex = 0; texelHeightIndex < height; ++texelHeightIndex)
								{
									// We can take advantage of the build order to average on the leading edges (right and top)
									// and then copy form the trailing edges (left and bottom)
									if(texelWidthIndex == 0 && leftTexture)
									{
										Color[] neighborTexels = leftTexture.GetPixels(mipIndex);
										alphaTexels[texelWidthIndex + texelHeightIndex * width] = neighborTexels[(width - 1) + (texelHeightIndex * width)];
									}
									else if(texelWidthIndex == width - 1 && rightTexture)
									{
										Color[] neighborTexels = rightTexture.GetPixels(mipIndex);
										alphaTexels[texelWidthIndex + texelHeightIndex * width] += neighborTexels[0 + (texelHeightIndex * width)];
										alphaTexels[texelWidthIndex + texelHeightIndex * width] *= 0.5f;
									}
									else if(texelHeightIndex == 0 && bottomTexture)
									{
										Color[] neighborTexels = bottomTexture.GetPixels(mipIndex);
										alphaTexels[texelWidthIndex + texelHeightIndex * width] = neighborTexels[texelWidthIndex + ((height - 1) * width)];
									}
									else if(texelHeightIndex == height - 1 && topTexture)
									{
										Color[] neighborTexels = topTexture.GetPixels(mipIndex);
										alphaTexels[texelWidthIndex + texelHeightIndex * width] += neighborTexels[texelWidthIndex + (0 * width)];
										alphaTexels[texelWidthIndex + texelHeightIndex * width] *= 0.5f;
									}
								}
							}
							alphaTexture.SetPixels(alphaTexels, mipIndex);
						}
						alphaTexture.wrapMode = TextureWrapMode.Clamp;
						alphaTexture.Apply(false);
					}

					// Apply CTS on each terrain piece
					if (ctsComponent != null)
					{
						ApplyCTSSettingsToTerrain(newTerrain, ctsComponent, ctsType);
					}

					newTerrain.Flush();
				}
			}
		}

		EditorUtility.ClearProgressBar();

		// deactivate original terrain
        // Deleting is too risky, user might not have a backup & there might still be important game objects as childs
		if(splitTerrain)
		{
            terrain.gameObject.SetActive(false);
		}
	}

	public static void SectorizeConnected(Terrain terrain, bool createPortalGeo, bool includeStatic, bool includeDynamic, bool sortGaiaSpawns)
	{
		Dictionary<Terrain, Terrain> processedTerrains = new Dictionary<Terrain, Terrain>();

		List<SECTR_SectorChildCandidate> sectorChildCandidates = new List<SECTR_SectorChildCandidate>();
		_GetRoots(includeStatic, includeDynamic, sectorChildCandidates);
        if (sortGaiaSpawns)
        {
            _GetGaiaSpawns(sectorChildCandidates);
        }

		_SectorizeConnected(terrain, createPortalGeo, includeStatic, includeDynamic, processedTerrains, sectorChildCandidates);
	}

#endregion

#region Unity Interface
	private void OnEnable()
	{
		//m_dropAreaBg = SECTR_IntroWindow.LoadIcon("SECTR_DropAreaBG");
	}

	protected override void OnGUI()
	{
		base.OnGUI();

        if (m_dropAreaStyle == null)
		{
			m_dropAreaStyle = new GUIStyle("Box")
			{
				name = "DropArea",
				fixedHeight = 100f,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
			};
			//m_dropAreaStyle.normal.background = m_dropAreaBg;
		}

		if (m_sectorCountFieldStyle == null)
		{
			m_sectorCountFieldStyle = new GUIStyle(GUI.skin.textField)
			{
				alignment = TextAnchor.MiddleRight,
			};
		}

		Terrain[] terrains = (Terrain[])GameObject.FindObjectsOfType(typeof(Terrain));
		int numTerrains = terrains.Length;
		bool sceneHasTerrains = numTerrains > 0;
		bool selectedInSector = false;
		bool hasTerrainComposer = false;

		EditorGUILayout.BeginVertical();
        m_windowScrollPosition = EditorGUILayout.BeginScrollView(m_windowScrollPosition);

        DrawHeader("TERRAINS", ref m_sectorSearch, 100, true);
		Rect r = EditorGUILayout.BeginVertical();
		r.y -= lineHeight;
		m_listScrollPosition = EditorGUILayout.BeginScrollView(m_listScrollPosition);
		bool wasEnabled = GUI.enabled;
		GUI.enabled = false;
		GUI.Button(r, sceneHasTerrains ? "" : "Current Scene Has No Terrains");
		GUI.enabled = wasEnabled;
		Terrain newSelectedTerrain = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
		Event evt = Event.current;
		if (evt.type == EventType.MouseDown && evt.button == 0)
		{
			newSelectedTerrain = null;
		}

		for (int terrainIndex = 0; terrainIndex < numTerrains; ++terrainIndex)
		{
			Terrain terrain = terrains[terrainIndex];
			if (terrain.name.ToLower().Contains(m_sectorSearch.ToLower()))
			{
				bool selected = terrain == m_selectedTerrain;
				bool inSector = false;
				Transform parent = terrain.transform;
				while (parent != null)
				{
					if (parent.GetComponent<SECTR_Sector>())
					{
						inSector = true;
						if (selected)
						{
							selectedInSector = true;
						}
						break;
					}
					parent = parent.parent;
				}

				hasTerrainComposer |= terrain.GetComponent("TerrainNeighbors") != null;

				Rect clipRect = EditorGUILayout.BeginHorizontal();
				if (selected)
				{
					Rect selectionRect = clipRect;
					selectionRect.y += 1;
					selectionRect.height += 1;
					GUI.Box(selectionRect, "", selectionBoxStyle);
				}

				GUILayout.FlexibleSpace();
				elementStyle.normal.textColor = selected ? Color.white : UnselectedItemColor;
				elementStyle.alignment = TextAnchor.MiddleCenter;
				EditorGUILayout.LabelField(terrain.name, elementStyle);
				GUILayout.FlexibleSpace();

				EditorGUILayout.EndHorizontal();

				if (evt.type == EventType.MouseDown && evt.button == 0 &&
					clipRect.Contains(evt.mousePosition))
				{
					newSelectedTerrain = terrain;
					selectedInSector = inSector;

					if (evt.clickCount == 2)
					{
						// Double click event
						if (SceneView.lastActiveSceneView)
						{
							SceneView.lastActiveSceneView.FrameSelected();
						}
					}
				}
			}
		}
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        bool doRepaint = false;
		if (newSelectedTerrain != m_selectedTerrain)
		{
			m_selectedTerrain = newSelectedTerrain;
			Selection.activeGameObject = m_selectedTerrain ? m_selectedTerrain.gameObject : null;
			SetDefaultsForTerrain();
			doRepaint = true;
		}

		bool sectorizableSelection = sceneHasTerrains && m_selectedTerrain != null && !selectedInSector;

		DrawHeader("SETTINGS", true);

		EditorGUILayout.BeginVertical();
        // Either split terrain, or sectorize connected. Can't do both.


        if (!m_sectorizeConnected)
        {
            EditorGUI.BeginChangeCheck();
            {
                m_splitTerrain = EditorGUILayout.Toggle(SPLIT_TERRAIN_LABEL, m_splitTerrain);
            }
            if (EditorGUI.EndChangeCheck())
            {
                SetDefaultsForTerrain();
                // SectorizeConnected has to be off to get here, but turning off in case something real odd happens
                m_sectorizeConnected = false;
            }
        }



        if (m_splitTerrain)
		{
			SplitControls();
		}
		else
		{
			if (!m_sectorizeConnected)
			{
				// "Width/Length/Height Must be 1 to Sectorize Connected Terrains"
				NonSplitControls();
			}

			if (hasTerrainComposer)
			{
				EditorGUI.BeginChangeCheck();
				{
					m_sectorizeConnected = EditorGUILayout.Toggle(SECTORIZE_CONNECTED_LABEL, m_sectorizeConnected);
				}
				if (EditorGUI.EndChangeCheck() && m_sectorizeConnected)
				{
					// "Width/Length/Height Must be 1 to Sectorize Connected Terrains"
					m_sectorsPerWidth = 1;
					m_sectorsPerLength = 1;
					m_sectorsPerHeight = 1;
				}
			}
			else
			{
				// In case the user enables sectorizeConnected and then selects another
				// terrain with no TerrainComposer, or removes it from the currently selected
				m_sectorizeConnected = false;
			}
		}


		EditorGUILayout.Space();
#if GAIA_PRESENT
        m_sortGaiaObjects = EditorGUILayout.Toggle(SORT_GAIA_SPAWNS_LABEL, m_sortGaiaObjects);
        EditorGUILayout.Space();
#else
        m_sortGaiaObjects = false;
#endif


        bool canSplitTerrain = m_selectedTerrain != null/* &&
			m_sectorsPerWidth > 1 && m_sectorsPerLength > 1 && m_sectorsPerWidth == m_sectorsPerLength &&
			Mathf.IsPowerOfTwo(m_sectorsPerWidth) && Mathf.IsPowerOfTwo(m_sectorsPerLength) &&
			(m_selectedTerrain.terrainData.heightmapResolution - 1) / m_sectorsPerWidth >= 32*/;

		m_createPortalGeo = EditorGUILayout.Toggle(CREATE_PORTALS_MESH_LABEL, m_createPortalGeo);
		m_groupStaticObjects = EditorGUILayout.Toggle(GROUP_STATIC_LABEL, m_groupStaticObjects);
		m_groupDynamicObjects = EditorGUILayout.Toggle(GROUP_DYNAMIC_LABEL, m_groupDynamicObjects);
		EditorGUILayout.EndVertical();

		if (!m_selectedTerrain)
		{
			GUI.enabled = false;
			GUILayout.Button("Select Terrain To Sectorize");
			GUI.enabled = true;
		}
		else if (!sectorizableSelection && selectedInSector)
		{
			GUI.enabled = false;
			GUILayout.Button("Cannot Sectorize Terrain That Is Already In a Sector");
			GUI.enabled = false;
		}
		//else if (m_sectorizeConnected && m_splitTerrain)
		//{
		//	GUI.enabled = false;
		//	GUILayout.Button("Cannot both Split and Sectorize Connected Terrains");
		//	GUI.enabled = false;
		//}
		//else if (m_sectorizeConnected && (m_sectorsPerWidth != 1 || m_sectorsPerLength != 1 || m_sectorsPerHeight != 1))
		//{
		//	GUI.enabled = false;
		//	GUILayout.Button("Width/Length/Height Must be 1 to Sectorize Connected Terrains");
		//	GUI.enabled = false;
		//}
		//else if (m_splitTerrain && m_sectorsPerWidth != m_sectorsPerLength)
		//{
		//	GUI.enabled = false;
		//	GUILayout.Button("Cannot split terrain unless Sectors Width and Length match.");
		//	GUI.enabled = true;
		//}
		//else if (m_splitTerrain && !Mathf.IsPowerOfTwo(m_sectorsPerWidth))
		//{
		//	GUI.enabled = false;
		//	GUILayout.Button("Cannot split terrain unless Sectors Width and Length are powers of 2.");
		//	GUI.enabled = true;
		//}
		//else if (m_splitTerrain && (m_selectedTerrain.terrainData.heightmapResolution - 1) / m_sectorsPerWidth < 32)
		//{
		//	GUI.enabled = false;
		//	GUILayout.Button("Cannot split terrain into chunks less than 32 x 32.");
		//	GUI.enabled = true;
		//}
		else if (GUILayout.Button("Sectorize Terrain"))
		{
			if (m_sectorizeConnected)
			{
				SectorizeConnected(m_selectedTerrain, m_createPortalGeo, m_groupStaticObjects, m_groupDynamicObjects, m_sortGaiaObjects);
				doRepaint = true;
			}
			else if (!m_splitTerrain || m_selectedTerrain.lightmapIndex < 0 || LightmapSettings.lightmaps.Length == 0 || EditorUtility.DisplayDialog("Lightmap Warning", "Splitting terrain will not preserve lightmaps. They will need to be rebaked. Continue sectorization?", "Yes", "No"))
			{
				SectorizeTerrain(m_selectedTerrain, m_sectorsPerWidth, m_sectorsPerLength, m_sectorsPerHeight, canSplitTerrain && m_splitTerrain, m_createPortalGeo, m_groupStaticObjects, m_groupDynamicObjects, m_sortGaiaObjects);
				doRepaint = true;
			}
		}
		GUI.enabled = wasEnabled;

		DropToSectorize();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        
        

        if (doRepaint)
		{
			Repaint();
		}
	}

	private void SplitControls()
	{
		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.PrefixLabel(SPLIT_POWER_LABEL, GUI.skin.horizontalSlider, EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			{
				m_terrainSplitPower = Mathf.RoundToInt(GUILayout.HorizontalSlider(m_terrainSplitPower, m_maxTerrainDivisionPower, 0f));
			}
			if (EditorGUI.EndChangeCheck())
			{
				m_sectorsPerWidth = (int)Mathf.Pow(2, m_terrainSplitPower);
				if (m_sectorsPerLength != m_sectorsPerWidth)
				{
					m_sectorsPerLength = m_sectorsPerWidth;

					UpdateSplitResults();
				}
			}
		}
		EditorGUILayout.EndHorizontal();

		EditorGUI.indentLevel += 2;
		EditorGUILayout.LabelField(PIECES_LABEL, m_piecesContent);
		EditorGUILayout.LabelField(SPLIT_SIZE_LABEL, m_newSize);
		EditorGUI.indentLevel -= 2;

		GUILayout.Label("Resolution of each piece", EditorStyles.boldLabel);
		EditorGUI.indentLevel += 2;
		EditorGUILayout.LabelField(HEIGHT_RESOLUTION_LABEL, m_newHeightRes);
		EditorGUILayout.LabelField(DETAIL_RESOLUTION_LABEL, m_newDetailRes);
		EditorGUILayout.LabelField(CONTROL_RESOLUTION_LABEL, m_newSplatRes);
		EditorGUILayout.LabelField(BASE_RESOLUTION_LABEL, m_newBaseRes);
		EditorGUI.indentLevel -= 2;

		EditorGUILayout.Space();

	}

	private void UpdateSplitResults()
	{

		if (m_selectedTerrain != null)
		{
			float newSize = m_selectedTerrain.terrainData.size.x / m_sectorsPerWidth;
			int newHeightRes = (int)((m_selectedTerrain.terrainData.heightmapResolution - 1) / m_sectorsPerWidth) + 1;
			int newDetailRes = (int)(m_selectedTerrain.terrainData.detailResolution / m_sectorsPerWidth);
			int newSplatRes = (int)(m_selectedTerrain.terrainData.alphamapResolution / m_sectorsPerWidth);
			int newBaseRes = (int)(m_selectedTerrain.terrainData.baseMapResolution / m_sectorsPerWidth);

			if (m_sectorsPerWidth == 1)
			{
				m_piecesContent = new GUIContent(m_sectorsPerWidth + " x " + m_sectorsPerLength + " (Single sector - No splitting)",
					"Leaving the terrain in 1 piece and creating 1 sector to contain it.");
			}
			else
			{
				m_piecesContent = new GUIContent(m_sectorsPerWidth + " x " + m_sectorsPerLength,
					"Splitting will create " + m_sectorsPerWidth + " x " + m_sectorsPerLength + " terrain pieces/sectors.");
			}
			m_newSize = new GUIContent(newSize + " x " + newSize + " x " + m_selectedTerrain.terrainData.size.y,
				"Size of each terrain piece will be " + newSize + " x " + newSize + " x " + m_selectedTerrain.terrainData.size.y + ".");

			m_newHeightRes = new GUIContent(newHeightRes.ToString(), "Heightmap resolution of each terrain piece will be " + newHeightRes);
			m_newDetailRes = new GUIContent(newDetailRes.ToString(), "Detail resolution of each terrain piece will be " + newDetailRes);
			m_newSplatRes = new GUIContent(newSplatRes.ToString(), "Control texture resolution of each terrain piece will be " + newSplatRes);
			m_newBaseRes = new GUIContent(newBaseRes.ToString(), "Base texture resolution of each terrain piece will be " + newBaseRes);
		}
		else
		{
			m_piecesContent = new GUIContent("No terrain selected");
			m_newSize = new GUIContent("No terrain selected");
			m_newHeightRes = new GUIContent("No terrain selected");
			m_newDetailRes = new GUIContent("No terrain selected");
			m_newSplatRes = new GUIContent("No terrain selected");
			m_newBaseRes = new GUIContent("No terrain selected");
		}
	}

	private void SetDefaultsForTerrain()
	{
        // Set defaults for terrain split
        //Only set defaults if 0, otherwise just leave what the user entered before
        if (m_sectorsPerWidth == 0)
        {
            m_sectorsPerWidth = m_selectedTerrain == null ? 1 : (m_selectedTerrain.terrainData.heightmapResolution - 1) / TARGET_SPLIT_RESOLUTION;
            m_sectorsPerWidth = m_sectorsPerWidth < 1 ? 1 : m_sectorsPerWidth;
        }
        // Get the power. Min scale is 1
        if (m_terrainSplitPower == 0)
        {
            m_terrainSplitPower = m_sectorsPerWidth <= 1 ? 0 : (int)Mathf.Log(m_sectorsPerWidth, 2);
        }
        if (m_maxTerrainDivisionPower == 0)
        {
            m_maxTerrainDivisionPower = m_selectedTerrain == null ? 2 : (m_selectedTerrain.terrainData.heightmapResolution - 1) / 32;
            m_maxTerrainDivisionPower = (int)Mathf.Log(m_maxTerrainDivisionPower, 2);
        }

		m_sectorsPerLength = m_sectorsPerWidth;
        // Could not think of a reason to split vertically
        if (m_terrainSplitPower == 0)
        {
            m_sectorsPerHeight = 1;
        }

		if(m_splitTerrain)
		{
			UpdateSplitResults();
		}
		else
		{
			UpdateNonSplitResults();
		}
	}

	private void NonSplitControls()
	{
		GUILayout.Label(SECTOR_COUNT_HEADER_LABEL, EditorStyles.boldLabel);

		EditorGUI.indentLevel += 4;
		EditorGUI.BeginChangeCheck();
		{
			m_sectorsPerWidth = EditorGUILayout.IntField(SECTORS_PER_WIDTH_LABEL, m_sectorsPerWidth);
			m_sectorsPerWidth = Mathf.Max(m_sectorsPerWidth, 1);
			m_sectorsPerLength = EditorGUILayout.IntField(SECTORS_PER_LENGTH_LABEL, m_sectorsPerLength);
			m_sectorsPerLength = Mathf.Max(m_sectorsPerLength, 1);
			m_sectorsPerHeight = EditorGUILayout.IntField(SECTORS_PER_HEIGHT_LABEL, m_sectorsPerHeight);
			m_sectorsPerHeight = Mathf.Max(m_sectorsPerHeight, 1);
		}
		if (EditorGUI.EndChangeCheck())
		{
			UpdateNonSplitResults();
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField(SPLIT_SIZE_LABEL, m_newSize);
		EditorGUI.indentLevel -= 4;

		EditorGUILayout.Space();
	}

	private void UpdateNonSplitResults()
	{
		if (m_selectedTerrain != null)
		{
			if (m_sectorsPerWidth * m_sectorsPerLength * m_sectorsPerHeight == 1)
			{
				m_newSize = new GUIContent(
					m_selectedTerrain.terrainData.size.x / m_sectorsPerWidth + " x " +
					m_selectedTerrain.terrainData.size.z / m_sectorsPerLength + " x " +
					m_selectedTerrain.terrainData.size.y / m_sectorsPerHeight + " (Single sector)",
					"Size of the sector will be the same as the terrain: " +
					m_selectedTerrain.terrainData.size.x / m_sectorsPerWidth + " x " +
					m_selectedTerrain.terrainData.size.z / m_sectorsPerLength + " x " +
					m_selectedTerrain.terrainData.size.y / m_sectorsPerHeight + ".");
			}
			else
			{
				m_newSize = new GUIContent(
					m_selectedTerrain.terrainData.size.x / m_sectorsPerWidth + " x " +
					m_selectedTerrain.terrainData.size.z / m_sectorsPerLength + " x " +
					m_selectedTerrain.terrainData.size.y / m_sectorsPerHeight,
					"Size of each sector will be " + m_selectedTerrain.terrainData.size.x / m_sectorsPerWidth + " x " +
					m_selectedTerrain.terrainData.size.z / m_sectorsPerLength + " x " +
					m_selectedTerrain.terrainData.size.y / m_sectorsPerHeight + ".");
			}
		}
		else
		{
			m_newSize = new GUIContent("No terrain selected");
		}
	}

	private void DropToSectorize()
	{
		SectionHeader(SECTORIZE_OBJECTS_LABEL, false);

		// Drop Options switch
		GUILayout.BeginHorizontal();
		{
			EditorGUILayout.PrefixLabel(REPARENTING_MODE_SWITCH_LABEL);
			m_dropReparentingMode = (SECTR_Constants.ReparentingMode)GUILayout.Toolbar((int)m_dropReparentingMode, REPARENTING_MODES_LABELS);
		}
		GUILayout.EndHorizontal();

		DrawSectorizeDropArea();
	}

	private void DrawSectorizeDropArea()
	{
		// Tooltips don't work on toolbar buttons in Unity 5.6 (and probably below)
		// Tested in 2017.1.0p4 and the tooltips work allright on the toolbar buttons
#if UNITY_5
		if (GUI.tooltip == REPARENTING_MODES_LABELS[0].tooltip || GUI.tooltip == REPARENTING_MODES_LABELS[1].tooltip)
		{
			GUILayout.Box(GUI.tooltip, new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft }, GUILayout.ExpandWidth(true));
			Repaint();
		}
		else
		{
			GUILayout.Box(DROP_AREA_LABEL, m_dropAreaStyle, GUILayout.ExpandWidth(true));
		}
#else
		GUILayout.Box(DROP_AREA_LABEL, m_dropAreaStyle, GUILayout.ExpandWidth(true));
#endif

		Rect dropArea = GUILayoutUtility.GetLastRect();
		Event evt = Event.current;

		switch (evt.type)
		{
			case EventType.DragUpdated:
			case EventType.DragPerform:
				if (!dropArea.Contains(evt.mousePosition))
				{
					return;
				}

				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				if (evt.type == EventType.DragPerform)
				{
					DragAndDrop.AcceptDrag();

					List<SECTR_SectorChildCandidate> sectorChildCandidates = new List<SECTR_SectorChildCandidate>();

					// Handle game objects
					foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
					{
						if (draggedObject is GameObject)
						{
							GameObject go = draggedObject as GameObject;
							if (go.GetComponent<SECTR_Portal>() != null)
							{
								Debug.LogWarning("Portal sectorization is not supported. Ignored GameObject '" + go.name + "'");
								continue;
							}
#if SECTR_STREAM_PRESENT
							else if (go.GetComponent<SECTR_Chunk>() != null)
							{
								Debug.LogWarning("Chunk sectorization is not supported. Ignored GameObject '" + go.name + "'");
								continue;
							}
#endif

							// Add them to list
							switch (m_dropReparentingMode)
							{
								case SECTR_Constants.ReparentingMode.Bounds:
									SECTR_SectorUtils.AddObjToCandidateListByBounds(ref sectorChildCandidates, go.transform);
									break;
								case SECTR_Constants.ReparentingMode.Position:
									SECTR_SectorUtils.AddObjToCandidateListByPosition(ref sectorChildCandidates, go.transform);
									break;
							}
						}
					}

					// Reparent them
					SECTR_SectorUtils.Encapsulate(sectorChildCandidates, "Sectorize objects");

                    //Do a final analysis and alert the user if one or more candidates were not encapsulated properly
                    List<SECTR_SectorChildCandidate> nonEncapsulated = GetNonEncapuslatedCandidates(sectorChildCandidates);
                    if (nonEncapsulated.Count > 0)
                    {
                        string dialogueText = "Not all GameObjects could be parented to a matching Sector. \n\n Usually this happens because the Object could not be matched according to the selected sorting mode. Please try a different sorting mode and / or control the following GameObjects:\n\n";

                        for (int i = 0; i <= Mathf.Min(nonEncapsulated.Count-1, 10); i++)
                        {
                            if (i < 10)
                            {
                                dialogueText += nonEncapsulated[i].transform.name + "\n";
                            }
                            else
                            {
                                dialogueText += "<and more...>";
                            }
                        }
                        EditorUtility.DisplayDialog("Not all Objects sorted", dialogueText, "OK");

                    }




				}
				break;
		}
	}

#region Private Interface

    private List<SECTR_SectorChildCandidate> GetNonEncapuslatedCandidates(List<SECTR_SectorChildCandidate> sectorChildCandidates)
    {
        List<SECTR_SectorChildCandidate> returnList = new List<SECTR_SectorChildCandidate>();
        foreach (SECTR_SectorChildCandidate candidate in sectorChildCandidates)
        {
            if (candidate.transform.parent != null)
            {

                SECTR_Sector sector = candidate.transform.parent.GetComponent<SECTR_Sector>();
                //Not child of a sector? Need to add that to the list.
                if (!sector)
                {
                    returnList.Add(candidate);
                }
            }
            else
            {
                //Not parent at all? Need to add that to the list.
                returnList.Add(candidate);
            }
        }

        return returnList;
    }
#endregion

   
    private static void _CreatePortal(bool createGeo, SECTR_Sector front, SECTR_Sector back, Transform parent, string undoString)
	{
		if(front && back)
		{
			string portalName = "SECTR Terrain Portal";
			GameObject newPortalObject;
			SECTR_Portal newPortal;
			if(createGeo)
			{
				newPortalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
				newPortalObject.name = portalName;
				Mesh quadResource = newPortalObject.GetComponent<MeshFilter>().sharedMesh;
				GameObject.DestroyImmediate(newPortalObject.GetComponent<MeshFilter>());
				GameObject.DestroyImmediate(newPortalObject.GetComponent<MeshRenderer>());
				GameObject.DestroyImmediate(newPortalObject.GetComponent<Collider>());
				newPortal = newPortalObject.AddComponent<SECTR_Portal>();
				newPortal.HullMesh = quadResource;
			}
			else
			{
				newPortalObject = new GameObject(portalName);
				newPortal = newPortalObject.AddComponent<SECTR_Portal>();
			}
			newPortal.SetFlag(SECTR_Portal.PortalFlags.PassThrough, true);
			newPortal.FrontSector = front;
			newPortal.BackSector = back;
			newPortal.transform.parent = parent;
			newPortal.transform.position = (front.TotalBounds.center + back.TotalBounds.center) * 0.5f;
			if(createGeo)
			{
				newPortal.transform.LookAt(back.TotalBounds.center);
				Vector3 orientation = newPortal.transform.forward;
				if(Mathf.Abs(orientation.x) >= Mathf.Abs(orientation.y) && Mathf.Abs(orientation.x) >= Mathf.Abs(orientation.z))
				{
					newPortal.transform.localScale = new Vector3(front.TotalBounds.size.z, front.TotalBounds.size.y, 1f);
				}
				else if(Mathf.Abs(orientation.y) >= Mathf.Abs(orientation.x) && Mathf.Abs(orientation.y) >= Mathf.Abs(orientation.z))
				{
					newPortal.transform.localScale = new Vector3(front.TotalBounds.size.x, front.TotalBounds.size.z, 1f);
				}
				else if(Mathf.Abs(orientation.z) >= Mathf.Abs(orientation.x) && Mathf.Abs(orientation.z) >= Mathf.Abs(orientation.y))
				{
					newPortal.transform.localScale = new Vector3(front.TotalBounds.size.x, front.TotalBounds.size.y, 1f);
				}
			}
			else
			{
				newPortal.transform.LookAt(front.TotalBounds.center);
			}
			SECTR_Undo.Created(newPortalObject, undoString);
		}
	}

	private static void _GetRoots(bool includeStatic, bool includeDynamic, List<SECTR_SectorChildCandidate> sectorChildCandidates)
	{
		if(includeStatic || includeDynamic)
		{
			Transform[] allTransforms = (Transform[])GameObject.FindObjectsOfType(typeof(Transform));
			foreach(Transform transform in allTransforms)
			{
				if(transform.parent == null &&
					((transform.gameObject.isStatic && includeStatic) || !transform.gameObject.isStatic && includeDynamic))
				{
					SECTR_SectorUtils.AddObjToCandidateListByBounds(ref sectorChildCandidates, transform);
				}
			}
		}
	}

	private static void _GetGaiaSpawns(List<SECTR_SectorChildCandidate> sectorChildCandidates)
	{

        //Find all potential locations for Game Object spawns
        List<GameObject> spawnedGOContainers = new List<GameObject>();
        //Gaia 1
        spawnedGOContainers.AddRange(Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name == "Spawned_GameObjects").ToList());
        //Gaia 2 / Pro
        spawnedGOContainers.AddRange(Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name == "Gaia Game Object Spawns").ToList());

        if (spawnedGOContainers == null)
		{
			// The scene doesn't contain Gaia Spawned game objects, or things were renamed/reorganised
			return;
		}

        // Iterate through and work out what needs to be reparented
        foreach (GameObject goContainer in spawnedGOContainers)
        {
            Transform parent = null;
            for (int i = goContainer.transform.childCount - 1; i >= 0; i--)
            {
                parent = goContainer.transform.GetChild(i);
                //only process active elements
                if (parent.gameObject.activeInHierarchy)
                {
                    for (int j = parent.childCount - 1; j >= 0; j--)
                    {
                        var child = parent.GetChild(j);
                        //only process active elements
                        if (child.gameObject.activeInHierarchy)
                        {
                            SECTR_SectorUtils.AddObjToCandidateListByPosition(ref sectorChildCandidates, child, new string[] { parent.name, SECTR_Constants.GAIA_SPAWN_GROUP });
                        }
                    }
                }
            }
        }
	}

	private static void _SectorizeConnected(Terrain terrain, bool createPortalGeo, bool includeStatic, bool includeDynamic, Dictionary<Terrain, Terrain> processedTerrains, List<SECTR_SectorChildCandidate> sectorChildCandidates)
	{
		if(terrain && !processedTerrains.ContainsKey(terrain))
		{
			string undoString = "Sectorize Connected";
			processedTerrains[terrain] = terrain;
			terrain.gameObject.isStatic = true;
			GameObject newSectorObject = new GameObject(terrain.name + " Sector");
			newSectorObject.isStatic = true;
			newSectorObject.transform.parent = terrain.transform.parent;
			newSectorObject.transform.localPosition = terrain.transform.localPosition;
			newSectorObject.transform.localRotation = terrain.transform.localRotation;
			newSectorObject.transform.localScale = terrain.transform.localScale;
			terrain.transform.parent = newSectorObject.transform;
			SECTR_Sector newSector = newSectorObject.AddComponent<SECTR_Sector>();
			newSector.ForceUpdate(true);
			SECTR_Undo.Created(newSectorObject, undoString);
			SECTR_SectorUtils.Encapsulate(newSector, sectorChildCandidates, undoString);

			Component terrainNeighbors = terrain.GetComponent("TerrainNeighbors");
			if(terrainNeighbors)
			{
				System.Type neighborsType = terrainNeighbors.GetType();
				Terrain topTerrain = neighborsType.GetField("top").GetValue(terrainNeighbors) as Terrain;
				if(topTerrain)
				{
					SECTR_Sector neighborSector = topTerrain.transform.parent ? topTerrain.transform.parent.GetComponent<SECTR_Sector>() : null;
					if(neighborSector)
					{
						newSector.TopTerrain = neighborSector;
						neighborSector.BottomTerrain = newSector;
						_CreatePortal(createPortalGeo, newSector, neighborSector, newSectorObject.transform.parent, undoString);
					}
					_SectorizeConnected(topTerrain, createPortalGeo, includeStatic, includeDynamic, processedTerrains, sectorChildCandidates);
				}
				Terrain bottomTerrain = neighborsType.GetField("bottom").GetValue(terrainNeighbors) as Terrain;
				if(bottomTerrain)
				{
					SECTR_Sector neighborSector = bottomTerrain.transform.parent ? bottomTerrain.transform.parent.GetComponent<SECTR_Sector>() : null;
					if(neighborSector)
					{
						newSector.BottomTerrain = neighborSector;
						neighborSector.TopTerrain = newSector;
						_CreatePortal(createPortalGeo, newSector, neighborSector, newSectorObject.transform.parent, undoString);
					}
					_SectorizeConnected(bottomTerrain, createPortalGeo, includeStatic, includeDynamic, processedTerrains, sectorChildCandidates);
				}
				Terrain leftTerrain = neighborsType.GetField("left").GetValue(terrainNeighbors) as Terrain;
				if(leftTerrain)
				{
					SECTR_Sector neighborSector = leftTerrain.transform.parent ? leftTerrain.transform.parent.GetComponent<SECTR_Sector>() : null;
					if(neighborSector)
					{
						newSector.LeftTerrain = neighborSector;
						neighborSector.RightTerrain = newSector;
						_CreatePortal(createPortalGeo, newSector, neighborSector, newSectorObject.transform.parent, undoString);
					}
					_SectorizeConnected(leftTerrain, createPortalGeo, includeStatic, includeDynamic, processedTerrains, sectorChildCandidates);
				}
				Terrain rightTerrain = neighborsType.GetField("right").GetValue(terrainNeighbors) as Terrain;
				if(rightTerrain)
				{
					SECTR_Sector neighborSector = rightTerrain.transform.parent ? rightTerrain.transform.parent.GetComponent<SECTR_Sector>() : null;
					if(neighborSector)
					{
						newSector.RightTerrain = neighborSector;
						neighborSector.LeftTerrain = newSector;
						_CreatePortal(createPortalGeo, newSector, neighborSector, newSectorObject.transform.parent, undoString);
					}
					_SectorizeConnected(rightTerrain, createPortalGeo, includeStatic, includeDynamic, processedTerrains, sectorChildCandidates);
				}
			}
		}
	}

	private static void ApplyCTSSettingsToTerrain(Terrain terrain, Component ctsOriginalComponent, Type ctsType)
	{
		
        
        Type ctsTerrainManagerType = SECTR_Utils.GetType("CTS.CTSTerrainManager");

        //CTSTerrainManager is a singleton & not always present in hierarchy, but will be created when instance is accessed anyways

        //var ctsTerrainManager = GameObject.FindObjectOfType(ctsTerrainManagerType);

        //if (ctsTerrainManagerType == null || ctsTerrainManager == null)
        //{
        //	// CTS component is supposedly there but could not find the manager
        //	Debug.LogWarning("No CTSTerrainManager was found.");
        //	return;
        //}

        var ctsTerrainManagerInstance = ctsTerrainManagerType
			.GetProperty("Instance",
						System.Reflection.BindingFlags.FlattenHierarchy
						| System.Reflection.BindingFlags.Static
						| System.Reflection.BindingFlags.Public)
                        .GetValue(null, null);
			//.GetValue(ctsTerrainManager, null);

		if (ctsTerrainManagerInstance == null)
		{
			Debug.LogWarning("Could not access the CTSTerrainManager instance.");
			return;
		}

		//CTS.CTSTerrainManager.Instance.AddCTSToTerrain(terrain);
		ctsTerrainManagerType.GetMethod("AddCTSToTerrain").Invoke(
			ctsTerrainManagerInstance,
			new object[] { terrain }
			);

		var newComponent = terrain.GetComponent(ctsType);
		HashSet<string> propertiesToCopy = new System.Collections.Generic.HashSet<string> {
				"AutoBakeNormalMap",
				"AutoBakeColorMap",
				"AutoBakeGrassIntoColorMap",
				"AutoBakeGrassMixStrength",
				"AutoBakeGrassDarkenAmount",
				//Don't copy cutout settings, users will need to re-enable this manually and provide matching cutout masks
                //"UseCutout",
				//"CutoutHeight",
			};
		foreach (var property in ctsType.GetProperties())
		{
			if (propertiesToCopy.Contains(property.Name))
			{
				property.SetValue(newComponent, property.GetValue(ctsOriginalComponent, null), null);
			}
		}

		//CTS.CTSTerrainManager.Instance.BroadcastProfileSelect(ctsOriginalComponent.profile, terrain);
		var profileField = ctsType.GetField("m_profile",
				System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.NonPublic
				);
		if (profileField == null)
		{
			Debug.LogWarning("Could not get the profile field of the Complete " +
				"Terrain Shader of the terrain '" + terrain.name + "'.");
			return;
		}
		Type ctsProfileType = SECTR_Utils.GetType("CTS.CTSProfile");
		if (ctsProfileType == null)
		{
			Debug.LogWarning("Could not get type CTSProfile.");
			return;
		}
		ctsTerrainManagerType
			.GetMethod("BroadcastProfileSelect", new Type[] { ctsProfileType, typeof(Terrain) })
			.Invoke(
				ctsTerrainManagerInstance,
				new object[] { profileField.GetValue(ctsOriginalComponent), terrain }
			);
	}
#endregion
}
