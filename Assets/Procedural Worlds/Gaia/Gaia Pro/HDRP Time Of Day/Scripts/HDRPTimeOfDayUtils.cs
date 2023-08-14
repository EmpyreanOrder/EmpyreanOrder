using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace ProceduralWorlds.HDRPTOD
{
    public class HDRPTimeOfDayUtils
    {
        public static string GetCurrentScriptingDefines()
        {
#if UNITY_EDITOR
#if UNITY_2021_3_OR_NEWER
            string symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
                string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            return symbols;
#else
            return "";
#endif

        }


        public static void SetCurrentScriptingDefines(string symbols)
        {
#if UNITY_EDITOR
#if UNITY_2021_3_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), symbols);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
#endif
#endif
        }

        /// <summary>
        /// Wrapper to deal with the "Find Object of Type" API changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T FindOOT<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_3_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        // <summary>
        /// Wrapper to deal with the "Find Object of Type" API changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T[] FindOOTs<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_3_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}
