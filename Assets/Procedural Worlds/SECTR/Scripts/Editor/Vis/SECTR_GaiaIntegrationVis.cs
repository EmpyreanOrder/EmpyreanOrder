#if GAIA_PRESENT && UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaia.GX.ProceduralWorlds
{
    /// <summary>
    /// SECTR Vis workflow for Gaia - Terrain sectorization
    /// </summary>
    public partial class SECTRGaiaIntegratiion : MonoBehaviour
	{
		private const string VIS_ABOUT = "\n" +
			"Click [Add Camera Culling] to add this functionality to your controller." +
			"\n\nNote: Depending on your setup, Camera Culling could reduce performance when using " +
			"with Stream. It's recommended to test your scenario and use one or the other when " +
			"necessary." +
			"\n";

		#region Generic informational methods

		// Already done in Core

		#endregion

		#region Methods exposed by Gaia as buttons must be prefixed with GX_

		public static void GX_Vis_About()
		{
			EditorUtility.DisplayDialog("About SECTR VIS", VIS_ABOUT, "OK");
		}

		/// <summary>
		/// Adds culling camera to a controler.
		/// </summary>
		public static void GX_Vis_AddCameraCulling()
		{
			AddCameraCulling();
		}

		#endregion

		#region Helper methods


		private static void AddCameraCulling()
		{
			Camera camera = Camera.main;
			if (camera == null)
			{
				camera = FindObjectOfType<Camera>();
			}
			if (camera == null)
			{
				EditorUtility.DisplayDialog("OOPS!", "Could not find the controller with a camera to add the culling camera to. Please add a controller/camera to your scene.", "OK");
				return;
			}

			GameObject go = camera.gameObject;
			if (go == null)
			{
				EditorUtility.DisplayDialog("OOPS!", "Found a camera but it doesn't seem to belong to a game object. Please add a controller/camera to your scene.", "OK");
				return;
			}

			Selection.activeGameObject = go;

			string msg;
			SECTR_CullingCamera cullingCamComponent = go.GetComponent<SECTR_CullingCamera>();
			if (cullingCamComponent != null)
			{
				msg = "> Game Object '" + go.name + "' already has Camera Culling.";
			}
			else
			{
				cullingCamComponent = go.AddComponent<SECTR_CullingCamera>();
				msg = "> Camera Culling was added to '" + go.name + "'.";
			}

			if (cullingCamComponent != null)
			{
				if (EditorUtility.DisplayDialog("SECTR Vis", msg + "\n\nDo you need multiple cameras to be active with Camera Culling simultaneously?", "No", "Yes"))
				{
					cullingCamComponent.MultiCameraCulling = false;
					msg += "\n> Disabled 'Multi Camera Culling' to optimize performance. Enable any time when needed.";
				}
				else
				{
					cullingCamComponent.MultiCameraCulling = true;
					msg += "\n> 'Multi Camera Culling' is enabled. Disable to optimize performance when you no longer need it.";
				}
				
				if (EditorUtility.DisplayDialog("SECTR Vis", msg + "\n\nWould you like to monitor culling in the Scene View for testing?", "Yes", "No"))
				{
					cullingCamComponent.CullInEditor = true;
					msg += "\n> Enabled 'Cull In Editor'. Disable once testing is done.";
				}
				else
				{
					cullingCamComponent.CullInEditor = false;
					msg += "\n> 'Cull In Editor' is disabled. You can enable it any time while testing culling.";
				}
			}
			else
			{
				msg += "\n> Unable to access Camera Culling settings on '" + go.name + "'.";
			}
			
			EditorUtility.DisplayDialog("SECTR Vis", msg, "OK");
		}

		#endregion
	}
}

#endif
