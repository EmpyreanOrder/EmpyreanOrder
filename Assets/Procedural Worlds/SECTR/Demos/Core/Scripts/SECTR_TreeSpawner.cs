using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SECTR_TreeSpawner : MonoBehaviour {

    public GameObject treeToSpawn;
    public float spawnThreshold = 0.5f;
    public bool spawnEnabled = true;


    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}


    private void OnDrawGizmos()
    {
        if (spawnEnabled)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {

                //only spawn trees if no prototypes on terrain
                if (t.terrainData.treePrototypes.Length == 0)
                {
                    Debug.Log("Generating new trees for terrain " + t.name);
                    TreePrototype tp = new TreePrototype() { prefab = treeToSpawn, bendFactor = 0f };
                    TreePrototype[] tpArray = new TreePrototype[1];
                    tpArray[0] = tp;
                    t.terrainData.treePrototypes = tpArray;
                    for (float x = 0; x < t.terrainData.size.x; x++)
                    {
                        for (float z = 0; z < t.terrainData.size.x; z++)
                        {
                            if (UnityEngine.Random.value >= spawnThreshold)
                            {
                                TreeInstance treeTemp = new TreeInstance();
                                treeTemp.position = new Vector3(x / t.terrainData.size.x, 0, z / t.terrainData.size.z);
                                treeTemp.prototypeIndex = 0;
                                treeTemp.widthScale = 1f;
                                treeTemp.heightScale = 1f;
                                treeTemp.color = Color.white;
                                treeTemp.lightmapColor = Color.white;
                                t.AddTreeInstance(treeTemp);

                            }
                        }
                    }
                }
            }
            spawnEnabled = false;
        }
    }

}
