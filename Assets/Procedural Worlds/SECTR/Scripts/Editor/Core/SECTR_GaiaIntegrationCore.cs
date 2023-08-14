#if GAIA_PRESENT && UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaia.GX.ProceduralWorlds
{
    /// <summary>
    /// SECTR Core workflow for Gaia - Terrain sectorization
    /// </summary>
    public partial class SECTRGaiaIntegratiion : MonoBehaviour
    {
		private const string CORE_ABOUT = "\n" +
			"Click on [Sectorize] to sectorize your terrain." +
			"\n\nIn the SECTR Terrain window you will also be able to easily move objects of " +
			"your selection into their own sectors." +
			"\n";

		#region Generic informational methods

		/// <summary>
		/// Returns the publisher name if provided. 
		/// This will override the publisher name in the namespace ie Gaia.GX.PublisherName
		/// </summary>
		/// <returns>Publisher name</returns>
		public static string GetPublisherName()
        {
            return "Procedural Worlds";
        }

        /// <summary>
        /// Returns the package name if provided
        /// This will override the package name in the class name ie public class PackageName.
        /// </summary>
        /// <returns>Package name</returns>
        public static string GetPackageName()
        {
            return "SECTR";
        }

		#endregion

		#region Methods exposed by Gaia as buttons must be prefixed with GX_

#if !SECTR_AUDIO_PRESENT
		// Need the double underscore otherwise it will throw errors due to the
		// duplication of the GX_About method and never gets to defining SECTR_AUDIO_PRESENT
		public static void GX__About()
        {
            EditorUtility.DisplayDialog("About SECTR", SECTR_Constants.GX_ABOUT, "OK");
		}
#endif

		public static void GX_Core_About()
		{
			EditorUtility.DisplayDialog("About SECTR", CORE_ABOUT, "OK");
		}

		/// <summary>
		/// Opens SECTR Terrain Window for the user to sectorize the terrain.
		/// </summary>
		public static void GX_Core_Sectorize()
		{
			// Select the terrain
			Terrain terrain = Gaia.TerrainHelper.GetActiveTerrain();
			if (terrain == null)
			{
				EditorUtility.DisplayDialog("OOPS!", "Could not find a terrain. Please follow the steps on the 'Standard' tab to create your terrain first.", "OK");
				return;
			}
			Selection.activeGameObject = terrain.gameObject;
			// And open the window
			EditorWindow.GetWindow<SECTR_TerrainWindow>();
		}

		#endregion

		#region Helper methods


		#endregion
	}
}

#endif
