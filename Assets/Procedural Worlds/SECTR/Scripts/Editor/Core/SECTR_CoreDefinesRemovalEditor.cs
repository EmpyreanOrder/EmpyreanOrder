using UnityEditor;

// Automates removal of SECTR Audio defines
public class SECTR_CoreDefinesRemovalEditor : UnityEditor.AssetModificationProcessor
{
	public static AssetDeleteResult OnWillDeleteAsset(string AssetPath, RemoveAssetOptions rao)
	{
		// Assuming that if this file is being removed, than the whole module is being removed
		if (AssetPath.EndsWith("SECTR") || AssetPath.EndsWith("SECTR/Scripts/Core"))
		{
			string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
			if (symbols.Contains("SECTR_CORE_PRESENT"))
			{
				symbols = symbols.Replace("SECTR_CORE_PRESENT;", "");
				symbols = symbols.Replace("SECTR_CORE_PRESENT", "");
				PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
			}
		}
		return AssetDeleteResult.DidNotDelete;
	}
}
