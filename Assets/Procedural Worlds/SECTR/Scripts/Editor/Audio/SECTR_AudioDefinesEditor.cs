using UnityEditor;

/// <summary>
/// Injects SECTR Audio Defines into project
/// </summary>
[InitializeOnLoad]
public class SECTR_AudioDefinesEditor : Editor
{
	static SECTR_AudioDefinesEditor()
	{
		// Make sure we inject SECTR_AUDIO_PRESENT
		string currBuildSettings = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
		if (!currBuildSettings.Contains("SECTR_AUDIO_PRESENT"))
		{
			PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings + ";SECTR_AUDIO_PRESENT");
		}
	}
}
