
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.IO;

public class SECTR_Utils : MonoBehaviour {

	#region Adhoc helpers

	/// <summary>
	/// Get the specified type if it exists
	/// </summary>
	/// <param name="TypeName">Name of the type to load</param>
	/// <returns>Selected type or null</returns>
	public static Type GetType(string TypeName)
	{

		// Try Type.GetType() first. This will work with types defined
		// by the Mono runtime, in the same assembly as the caller, etc.
		var type = Type.GetType(TypeName);

		// If it worked, then we're done here
		if (type != null)
			return type;

		// If the TypeName is a full name, then we can try loading the defining assembly directly
		if (TypeName.Contains("."))
		{
			// Get the name of the assembly (Assumption is that we are using 
			// fully-qualified type names)
			var assemblyName = TypeName.Substring(0, TypeName.IndexOf('.'));

			// Attempt to load the indicated Assembly
			try
			{
				var assembly = Assembly.Load(assemblyName);
				if (assembly == null)
					return null;

				// Ask that assembly to return the proper Type
				type = assembly.GetType(TypeName);
				if (type != null)
					return type;
			}
			catch (Exception)
			{
				//Debug.Log("Unable to load assemmbly : " + ex.Message);
			}
		}

		// If we still haven't found the proper type, we can enumerate all of the 
		// loaded assemblies and see if any of them define the type
		var currentAssembly = Assembly.GetCallingAssembly();
		{
			// Load the referenced assembly
			if (currentAssembly != null)
			{
				// See if that assembly defines the named type
				type = currentAssembly.GetType(TypeName);
				if (type != null)
					return type;
			}

		}

		//All loaded assemblies
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int asyIdx = 0; asyIdx < assemblies.GetLength(0); asyIdx++)
		{
			type = assemblies[asyIdx].GetType(TypeName);
			if (type != null)
			{
				return type;
			}
		}

		var referencedAssemblies = currentAssembly.GetReferencedAssemblies();
		foreach (var assemblyName in referencedAssemblies)
		{
			// Load the referenced assembly
			var assembly = Assembly.Load(assemblyName);
			if (assembly != null)
			{
				// See if that assembly defines the named type
				type = assembly.GetType(TypeName);
				if (type != null)
					return type;
			}
		}

		// The type just couldn't be found...
		return null;
	}


#if UNITY_EDITOR
    /// <summary>
    /// Return the Sectr directory in the project
    /// </summary>
    /// <returns>If in editor it returns the full cts directory, if in build, returns assets directory.</returns>
    public static string GetSectrDirectory()
    {
        string[] assets = AssetDatabase.FindAssets("Sectr_ReadMe", null);
        for (int idx = 0; idx < assets.Length; idx++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assets[idx]);
            if (Path.GetFileName(path) == "Sectr_ReadMe.txt")
            {
                return Path.GetDirectoryName(path) + "/";
            }
        }
        return "";
    }
#endif
#endregion

}
