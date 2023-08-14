using UnityEngine;

namespace Impostors.Samples
{
    [AddComponentMenu("")]
    public class SetTransitionsByDistance : MonoBehaviour
    {
        [SerializeField]
        private LODGroup _lodGroup = default;

        [SerializeField]
        private ImpostorLODGroup _impostorLODGroup = default;

        [SerializeField]
        private Camera _camera = default;

        [SerializeField]
        private float _impostorShowDistance = 100;

        [SerializeField]
        private float _impostorCullDistance = 1000;

        public void UpdateLODs()
        {
            var position = _impostorLODGroup.Position;
            var distance = (_camera.transform.position - position).magnitude;
            var height = _impostorLODGroup.LocalHeight;
            var multiplier =
                2 * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f) / QualitySettings.lodBias;
            float impostorShowScreenHeight = height / (_impostorShowDistance * multiplier);
            float impostorCullScreenHeight = height / (_impostorCullDistance * multiplier);

            var lods = _lodGroup.GetLODs();
            lods[lods.Length - 1].screenRelativeTransitionHeight = impostorShowScreenHeight;
            _lodGroup.SetLODs(lods);

            var ilods = _impostorLODGroup.LODs;
            ilods[0].screenRelativeTransitionHeight = impostorShowScreenHeight;
            ilods[ilods.Length - 1].screenRelativeTransitionHeight = impostorCullScreenHeight;
            _impostorLODGroup.SetLODsAndCache(ilods);
            _impostorLODGroup.RequestImpostorTextureUpdate();
        }


        private Vector3 _lastLocalScale;

        void Update()
        {
            if ((_lastLocalScale - transform.localScale).magnitude > float.Epsilon)
            {
                _lastLocalScale = transform.localScale;
                UpdateLODs();
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && Time.frameCount > 1)
                UpdateLODs();
        }
    }
}