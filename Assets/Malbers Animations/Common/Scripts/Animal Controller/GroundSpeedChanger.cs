using UnityEngine;

namespace MalbersAnimations.Controller
{
    [AddComponentMenu("Malbers/Animal Controller/Ground Speed Changer")]
    public class GroundSpeedChanger : MonoBehaviour
    {
        [Tooltip("Adittional Position added to the Movement on the Floor")]
        public float Position;

        [Tooltip("This will make the ground slippery if the value is very low")]
        public float Lerp = 2f;

        [Tooltip("Slide Override on the Animal Controller")]
        public float SlideAmount = 0.25f;

        [Tooltip("Slide activation using the Max Slope Limit")]
        public float SlideThreshold = 30f;
        [Tooltip("Slide activation using the Max Slope Limit")]
        public float SlideDamp = 20f;

        [Tooltip("Values used on the Slide State")]
        public SlideData SlideData;
    }

    [System.Serializable]
    public struct SlideData
    {
        public bool Slide;
        
        public bool IgnoreRotation;

        [Min(0)]public float MinAngle;
    }
}
