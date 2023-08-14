using UnityEngine;

namespace Impostors.Structs
{
    [System.Serializable]
    public struct ImpostorLOD
    {
        /// <summary>
        ///   <para>The screen relative height to use for the transition [0-1].</para>
        /// </summary>
        public float screenRelativeTransitionHeight;

        /// <summary>
        ///   <para>List of renderers for this LOD level.</para>
        /// </summary>
        public Renderer[] renderers;

        /// <summary>
        ///   <para>Construct a LOD.</para>
        /// </summary>
        /// <param name="screenRelativeTransitionHeight">The screen relative height to use for the transition [0-1].</param>
        /// <param name="renderers">An array of renderers to use for this LOD level.</param>
        public ImpostorLOD(float screenRelativeTransitionHeight, Renderer[] renderers)
        {
            this.screenRelativeTransitionHeight = screenRelativeTransitionHeight;
            this.renderers = renderers;
        }
    }
}