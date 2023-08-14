using UnityEngine;
using UnityEngine.UI;

namespace Impostors.Samples
{
    [AddComponentMenu("")]
    internal class UISliderWithValue : MonoBehaviour {

        public Slider slider = default;
        public Text text = default;
        public string unit = default;
        public byte decimals = 2;


        void OnEnable()
        {
            slider.onValueChanged.AddListener(ChangeValue);
            ChangeValue(slider.value);
        }
        void OnDisable()
        {
            slider.onValueChanged.RemoveAllListeners();
        }

        void ChangeValue(float value)
        {
            text.text = value.ToString("n" + decimals) + " " + unit;
        }
    }
}
