using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]
[assembly: InternalsVisibleTo("PerformanceTests")]
namespace Impostors.Samples
{
	[ExecuteInEditMode]
	[AddComponentMenu("")]
	internal class ExampleSpawner : MonoBehaviour
	{
        [SerializeField] bool _autoSpawn = false;
        [SerializeField] private int _autoSpawnCount = default;
		[SerializeField] List<GameObject> prefabs = new List<GameObject> ();
		[SerializeField] int count = 100;
		[SerializeField] bool button = false;
		[SerializeField] Vector3 randoms = new Vector3 (5f, 5f, 5f);
		[SerializeField] bool clear = false;

		public enum SpawnType
		{
			random,
			order
		}

		[SerializeField] SpawnType spawnType = default;

		void Start () {
            if (_autoSpawn && Application.isPlaying)
            {
	            StartCoroutine(Spawn(_autoSpawnCount));
            }
		}

		void Update () {
			if (button || Input.GetKeyDown (KeyCode.C) && prefabs != null) {
				button = false;
				switch (spawnType) {
					case SpawnType.random:
						RandomSpawn ();
						break;
					case SpawnType.order:
						for (int i = 0; i < 8; i++)
							OrderSpawn ();
						break;
				}
			}

			if (clear) {
				clear = false;
				foreach (Transform trans in transform)
					DestroyImmediate (trans.gameObject);
			}
		}

		void RandomSpawn () {
			for (int i = 0; i < count; i++) {
				Vector3 randomPos = transform.position + new Vector3 (Random.Range (-randoms.x, randoms.x), 1000, Random.Range (-randoms.z, randoms.z));
				RaycastHit hit;
				if (Physics.Raycast (randomPos, Vector3.down, out hit) && Vector3.Angle (hit.normal, Vector3.up) < 30) {
					Quaternion randomRot = Quaternion.Euler (0, Random.Range (0, 360), 0);
					GameObject go = (GameObject)Instantiate (GetRandomPrefab (), hit.point, randomRot);
					go.transform.parent = this.transform;
					go.name = go.name + " " + this.transform.childCount.ToString ();
				}
			}
		}

		[SerializeField] float offset = default;
		[SerializeField] int _orderSpawnCount = default;
		Quaternion randomRot;
		GameObject go;

		void OrderSpawn () {
			// x
			for (int i = 0; i < _orderSpawnCount; i++) {
				randomRot = Quaternion.Euler (0, Random.Range (0, 360), 0);
				go = (GameObject)Instantiate (GetRandomPrefab (), transform.position + new Vector3(i * offset, 0, _orderSpawnCount * offset), randomRot, transform);
				go.transform.parent = this.transform;
				go.name = go.name + " " + i + "x" + _orderSpawnCount;
				go.SetActive(true);
			}
			// x_z
			randomRot = Quaternion.Euler (0, Random.Range (0, 360), 0);
			go = (GameObject)Instantiate (GetRandomPrefab (), transform.position + new Vector3 (_orderSpawnCount * offset, 0, _orderSpawnCount * offset), randomRot);
			go.transform.parent = this.transform;
			go.name = go.name + " " + _orderSpawnCount +"x"+_orderSpawnCount;
			go.SetActive(true);
			// z
			for (int i = 0; i < _orderSpawnCount; i++) {
				randomRot = Quaternion.Euler (0, Random.Range (0, 360), 0);
				go = (GameObject)Instantiate (GetRandomPrefab (), transform.position + new Vector3 (_orderSpawnCount * offset, 0, i * offset), randomRot);
				go.transform.parent = this.transform;
				go.name = go.name + " " + _orderSpawnCount+"x"+i;
				go.SetActive(true);
			}
			_orderSpawnCount++;
		}

		GameObject GetRandomPrefab () {
			return prefabs [Random.Range (0, prefabs.Count)];
		}

		void Clear () {
			foreach (Transform trans in transform) {
				Destroy (trans.gameObject);
			}
			_orderSpawnCount = 0;
		}

		public void OnSpawn (int value) {
			Clear ();
			int spawnCount = 0;
			switch (value) {
				case 0:
					spawnCount = 64;
					break;
				case 1:
					spawnCount = 56;
					break;
				case 2:
					spawnCount = 48;
					break;
				case 3:
					spawnCount = 40;
					break;
				case 4:
					spawnCount = 32;
					break;
				case 5:
					spawnCount = 24;
					break;
				case 6:
					spawnCount = 16;
					break;
				case 7:
					spawnCount = 8;
					break;
			}
			Random.InitState(0);
			StartCoroutine (Spawn (spawnCount));
		}

		public IEnumerator Spawn (int spawnCount) {
			yield return null;
			for (int i = 0; i < spawnCount; i++)
				OrderSpawn ();
		}
	}
}


