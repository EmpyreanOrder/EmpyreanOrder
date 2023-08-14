#if GAIA_PRESENT && UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaia.GX.ProceduralWorlds
{
    /// <summary>
    /// SECTR Audio workflow for Gaia - Terrain sectorization
    /// </summary>
    public partial class SECTRGaiaIntegratiion : MonoBehaviour
	{
		private const string AUDIO_ABOUT = "\n" +
			"[Add Audio System]: You will need a SECTR Audio System in order to use SECTR Audio." +
			"\n\n[Open Audio Window]: The SECTR Audio Window is your one stop shop to work with all " +
			"your Audio Clips, Audio Cues, Audio Buses, and do your mixing. It is your Audio Headquarters." +
			"\n\n[Add Start Music] Adds a Start Music Game Object to your scene. You can add a Music Cue " +
			"(that should be 2D) to its Start Music component. The component will start up the music at the start" +
			" of the scene on a loop and will remove itself from the game." +
			"\n";

		#region Generic informational methods

		// Already done in Core

		#endregion

		#region Methods exposed by Gaia as buttons must be prefixed with 

		public static void GX_About()
		{
			EditorUtility.DisplayDialog("About SECTR", SECTR_Constants.GX_ABOUT, "OK");
		}

		public static void GX_Audio_About()
		{
			EditorUtility.DisplayDialog("About SECTR AUDIO", AUDIO_ABOUT, "OK");
		}

		/// <summary>
		/// Adds the SECTR Audio System to the controller.
		/// </summary>
		public static void GX_Audio_AddAudioSystem()
		{
			AddAudioSystem();
		}

		/// <summary>
		/// Opens the SECTR Audio Window.
		/// </summary>
		public static void GX_Audio_OpenAudioWindow()
		{
			EditorWindow.GetWindow<SECTR_AudioWindow>(true, "SECTR Audio", true);
		}

		/// <summary>
		/// Adds a start music object.
		/// </summary>
		public static void GX_Audio_AddStartMusic()
		{
			SECTR_AudioMenu.CreateStartMusic();
		}

		#endregion

		#region Helper methods

		private static void AddAudioSystem()
		{
			AudioListener[] listeners = GameObject.FindObjectsOfType<AudioListener>();

			if (listeners.Length < 1)
			{
				EditorUtility.DisplayDialog("OOPS!", "Could not find an audio listener. Please add an audio listener to your controller.", "OK");
				return;
			}

			GameObject go = null;

			if (listeners.Length == 1)
			{
				go = listeners[0].gameObject;
			}
			else
			{
				foreach (var listener in listeners)
				{
					GameObject listenerGo = listener.gameObject;
					string active = listenerGo.activeInHierarchy ? "Enabled" : "Disabled";
					if (EditorUtility.DisplayDialog("OOPS!", "There are more than one Audio Listeners in the Scene." +
						"\nConsider removing all but one, unless you have a good reason for having more." +
						"\nThis wizard will only add one Audio System." +
						"\n\nDo you want to add the Audio System to '" + listenerGo.name + "'(" + active + ")",
						"Yes", "No"))
					{
						go = listenerGo;
						break;
					}
				}
			}

			if (go == null)
			{
				EditorUtility.DisplayDialog("SECTR Audio", "No Game Object was selected to add the Audio System to. Aborted.", "OK");
				return;
			}

			Selection.activeGameObject = go;

			string msg;
			SECTR_AudioSystem component = go.GetComponent<SECTR_AudioSystem>();
			if (component != null)
			{
				msg = "> Game Object '" + go.name + "' already has Audio System component(s).";
			}
			else
			{
				component = go.AddComponent<SECTR_AudioSystem>();
				msg = "> Audio System was added to '" + go.name + "'.";
			}			
			
			EditorUtility.DisplayDialog("SECTR Audio", msg + "\n\nDon't forget to set your Master Bus(once you have one) in the Audio System.", "OK");
		}

		#endregion
	}
}

#endif
