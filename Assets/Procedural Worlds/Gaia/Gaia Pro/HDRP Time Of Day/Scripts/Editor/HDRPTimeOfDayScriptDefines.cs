using UnityEditor;
using UnityEngine.Rendering;

namespace ProceduralWorlds.HDRPTOD
{
    [InitializeOnLoad]
    public static class HDRPTimeOfDayScriptDefines
    {
        static HDRPTimeOfDayScriptDefines()
        {
#if !GAIA_2_PRESENT
            bool updateScripting = false;
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (IsHDRP())
            {
                if (!symbols.Contains("HDPipeline"))
                {
                    updateScripting = true;
                    if (symbols.Length > 0)
                    {
                        symbols += ";HDPipeline";
                    }
                    else
                    {
                        symbols += "HDPipeline";
                    }
                }
            }
            else
            {
                if (symbols.Contains("HDPipeline"))
                {
                    updateScripting = true;
                    symbols = symbols.Replace("HDPipeline", "");
                }
            }

            if (updateScripting && EditorUserBuildSettings.selectedBuildTargetGroup != BuildTargetGroup.Unknown)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
            }
#endif
        }

        /// <summary>
        /// Checks to see if HDRP is currently being used
        /// Returns false if it's built-in or URP pipeline
        /// </summary>
        /// <returns></returns>
        private static bool IsHDRP()
        {
            if (GraphicsSettings.renderPipelineAsset != null)
            {
                return GraphicsSettings.renderPipelineAsset.GetType().ToString().Contains("HDRenderPipelineAsset");
            }
            return false;
        }
    }
}