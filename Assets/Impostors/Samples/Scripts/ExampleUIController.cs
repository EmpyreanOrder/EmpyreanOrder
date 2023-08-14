using UnityEngine;
using UnityEngine.UI;

namespace Impostors.Samples
{
    /// <summary>
    /// Wrapper to share UI prefab between scenes
    /// </summary>
    [AddComponentMenu("")]
    internal class ExampleUIController : MonoBehaviour
    {
        [SerializeField]
        private Toggle _impostorsEnabledToggle;

        private void OnEnable()
        {
            _impostorsEnabledToggle.onValueChanged.AddListener(SetImpostorsEnabled);
        }

        public void Spawn(int value)
        {
            FindObjectOfType<ExampleSpawner>().OnSpawn(value);
        }

        public void SetPlayerMovementEnabled(bool value)
        {
            //FindObjectOfType<SimpleCameraController>().enabled = value;
        }

        public void SetImpostorsEnabled(bool value)
        {
            var ilods = FindObjectsOfType<ImpostorLODGroup>();
            foreach (var lodGroup in ilods)
            {
                lodGroup.enabled = value;
            }
        }
    }
}