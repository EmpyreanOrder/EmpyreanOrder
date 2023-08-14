using UnityEngine;

//namespace SECTR
//{
    public static class SECTR_Constants {
		/// <summary>
		/// Version information
		/// </summary>
		public const string MAJOR_VERSION = "1";
		public const string MINOR_VERSION = "4.0";

		/// <summary>
		/// The mode to be used when determining the parent sector of an object.
		/// <paramref name="Bounds"/> will look at the object's bounds (extent), including lights,
		/// and leave cross-sector objects in the global space.
		/// <paramref name="Position"/> will look at the object's transform position to determine its
		/// parent sector.
		/// </summary>
		public enum ReparentingMode { Bounds, Position };

		/// <summary>
		/// The color of the line that is used to separate sections.
		/// </summary>
		public static readonly Color UI_SEPARATOR_LINE_COLOR = new Color(0.455f, 0.455f, 0.455f);

		/// <summary>
		/// Name of the game object Gaia Spawned Game Objects should be grouped under
		/// </summary>
		public const string GAIA_SPAWN_GROUP = "Gaia_Spawns";

        /// <summary>
        /// Path to the audio graph material
        /// </summary>
        public const string PATH_AudioHUDGraphMaterial = "Scripts/Audio/Assets/AudioHUD_Graph.mat";

        /// <summary>
        /// Path to the vis frustrum debug material
        /// </summary>
        public const string PATH_VisGizmoMaterial = "Scripts/Vis/Assets/FrustumDebug.mat";

        /// <summary>
        /// Path to the Sectr Audio Icons
        /// </summary>
        public const string PATH_AudioIcons = "Scripts/Audio/Editor/Icons/";

        /// <summary>
        /// The about blurb of SECTR as a whole as shown in Gaia GX
        /// </summary>
    public const string GX_ABOUT = "\nSECTR is a suite of modules for Unity that allows you to build the best looking, " +
			"sounding, and most efficient games possible, all by taking advantage of the structure already present in " +
			"your game world. If you want to stream an open world, bring a huge game to mobile, or take advantage of " +
			"the latest techniques in audio occlusion and propagation, SECTR is your solution.\n\n" +
			"Main Features\n" +
			" - SECTR CORE: Sector Creation Kit\n" +
			" - SECTR AUDIO: Immersive Spatial Audio\n" +
			" - SECTR STREAM: Seamless Scene Streaming\n" +
			" - SECTR VIS: Dynamic Occlusion Culling\n\n" +
			" - SECTR COMPLETE: Contains all the packages\n";
}
//}

