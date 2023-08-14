using UnityEditor;

// Automates removal of SECTR Audio defines
public class SECTR_AudioDefinesRemovalEditor : UnityEditor.AssetModificationProcessor
{
	public static AssetDeleteResult OnWillDeleteAsset(string AssetPath, RemoveAssetOptions rao)
	{
		// Assuming that if this file is being removed, than the whole module is being removed
		if (AssetPath.EndsWith("SECTR") || AssetPath.EndsWith("SECTR/Scripts/Audio"))
		{
			string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
			if (symbols.Contains("SECTR_AUDIO_PRESENT"))
			{
				symbols = symbols.Replace("SECTR_AUDIO_PRESENT;", "");
				symbols = symbols.Replace("SECTR_AUDIO_PRESENT", "");
				PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
			}
		}
		return AssetDeleteResult.DidNotDelete;
	}
}
