// Copyright © 2018 Procedural Worlds Pty Limited.  All Rights Reserved.
using UnityEngine;
using UnityEditor;
using PWCommon5;

namespace SECTR.Internal
{
    public class SECTRStdMenu : Editor
    {
        /// <summary>
        /// Show tutorials
        /// </summary>
        [MenuItem("Window/" + PWConst.COMMON_MENU + "/SECTR/Show SECTR Tutorials...", false, 60)]
        public static void ShowTutorial()
        {
            Application.OpenURL(PWApp.CONF.TutorialsLink);
        }

        /// <summary>
        /// Show support page
        /// </summary>
        [MenuItem("Window/" + PWConst.COMMON_MENU + "/SECTR/Show SECTR forum, post your issues here...", false, 61)]
        public static void ShowSupport()
        {
            Application.OpenURL(PWApp.CONF.SupportLink);
        }

        /// <summary>
        /// Show review option
        /// </summary>
        [MenuItem("Window/" + PWConst.COMMON_MENU + "/SECTR/Please Review SECTR...", false, 62)]
        public static void ShowProductAssetStore()
        {
            Application.OpenURL(PWApp.CONF.ASLink);
        }

    }
}
