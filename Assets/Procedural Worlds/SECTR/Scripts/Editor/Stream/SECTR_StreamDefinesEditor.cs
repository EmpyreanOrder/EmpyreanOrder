using UnityEditor;

/// <summary>
/// Injects SECTR Audio Defines into project
/// </summary>
[InitializeOnLoad]
public class SECTR_StreamDefinesEditor : Editor
{
	static SECTR_StreamDefinesEditor()
	{
		// Make sure we inject SECTR_AUDIO_PRESENT
		string currBuildSettings = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);		
		if (!currBuildSettings.Contains("SECTR_STREAM_PRESENT"))
		{
			PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings + ";SECTR_STREAM_PRESENT");
		}
	}
}
