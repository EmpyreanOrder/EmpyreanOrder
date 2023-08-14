using UnityEngine;

namespace Impostors.Editor
{
    public class LODUtility
    {
        public static float CalculateDistance(Camera camera, float relativeScreenHeight, LODGroup lodGroup){
            float distance;
            if (camera.orthographic) {
                distance = lodGroup.size / relativeScreenHeight / 4f;
            } else {
                float _multiplier = 2 * Mathf.Tan ( camera.fieldOfView/2 * Mathf.Deg2Rad);
                relativeScreenHeight *= 2;
                distance = lodGroup.size / relativeScreenHeight / _multiplier;
            }
            return distance;
        }
    }
}