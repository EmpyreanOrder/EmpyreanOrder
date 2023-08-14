using UnityEditor;

/// <summary>
/// Injects SECTR Audio Defines into project
/// </summary>
[InitializeOnLoad]
public class SECTR_CoreDefinesEditor : Editor
{
	static SECTR_CoreDefinesEditor()
	{
		// Make sure we inject SECTR_AUDIO_PRESENT
		string currBuildSettings = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
		if (!currBuildSettings.Contains("SECTR_CORE_PRESENT"))
		{
			PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings + ";SECTR_CORE_PRESENT");
		}
	}
}
