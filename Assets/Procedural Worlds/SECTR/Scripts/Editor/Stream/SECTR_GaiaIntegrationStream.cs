#if GAIA_PRESENT && UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaia.GX.ProceduralWorlds
{
    /// <summary>
    /// SECTR Stream workflow for Gaia - Terrain sectorization
    /// </summary>
    public partial class SECTRGaiaIntegratiion : MonoBehaviour
	{
		private const string STREAM_ABOUT = "\n" +
            "Streaming allows you to split up larger scenes or terrains into smaller chunks to load them in around the player while your game is running. You can follow the steps outlined in the GX Stream Buttons to get your Gaia Terrain ready for streaming:\n\n" + 
			"1. The first step for Streaming is to select your loaders. " +
			"The [1 Select A Loader] button will help you choose your first loaders. Depending on your needs, " +
			"you can load in sectors in different styles. The region loader is recommended for streaming terrains." +
            "\n\n2. The next step is Sectorizing the Terrain. This will split up the terrain into smaller chunks to load during runtime." +
            "\n\n3. Step 3 is Exporting the sectors. Exporting the sectors will store their contents in separate scenes for streaming. Once the sectors are exported, you are ready " +
			"to start streaming." +
			"\n\n - Depending on your scenario, you might also need to do Light Mapping of your sectors. " +
			"You will do that in the same window ('Sector Stream Window' that opens when you click [2 Export Sectors])." +
			"\n\n - From the window you will also be able to reimport your sectors and export them once again, in case " +
			"you need to do some more work on them." +
			"\n\n - You also have the option to export a Sector Graph Visualization which generates a file with text " +
			"information on how the sectors are connected." +
			"\n";

		#region Generic informational methods

		// Already done in Core

		#endregion

		#region Methods exposed by Gaia as buttons must be prefixed with GX_

		public static void GX_Stream_About()
		{
			EditorUtility.DisplayDialog("About SECTR Stream", STREAM_ABOUT, "OK");
		}

        /// <summary>
        /// Options for the user to add SECTR loaders to the controller.
        /// </summary>
        public static void GX_Stream_1SelectALoader()
        {
            EditorWindow.GetWindow<SECTR_GaiaAddLoadersWindow>(true, "SECTR Stream", true);
        }


        /// <summary>
		/// Opens SECTR Terrain Window for the user to sectorize the terrain.
		/// </summary>
		public static void GX_Stream_2Sectorize()
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


		/// <summary>
		/// Opens SECTR Stream Window to allow the user to handle sector exporting.
		/// </summary>
		public static void GX_Stream_3ExportSectors()
		{
			EditorWindow.GetWindow<SECTR_StreamWindow>(true, "SECTR Stream", true);
		}

		#endregion

		#region Helper methods

		#endregion
	}
}

#endif
