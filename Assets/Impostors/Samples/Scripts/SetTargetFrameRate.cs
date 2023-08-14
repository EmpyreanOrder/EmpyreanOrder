using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Impostors.Samples
{
    [AddComponentMenu("")]
    internal class SetTargetFrameRate : MonoBehaviour
    {
        [SerializeField]
        private int _targetFrameRate = 1000;

        [Range(0f, 2f)]
        [SerializeField]
        private float _timeScale = 1f;

        [Range(1, 12)]
        [SerializeField]
        private int _jobWorkerCount = 4;

        [SerializeField]
        private NativeLeakDetectionMode _nativeLeakDetectionMode = NativeLeakDetectionMode.Disabled;

        private void Start()
        {
            UpdateSettings();
        }

        private void UpdateSettings()
        {
            NativeLeakDetection.Mode = _nativeLeakDetectionMode;
            _targetFrameRate = Mathf.Clamp(_targetFrameRate, 5, 1000);
            Application.targetFrameRate = _targetFrameRate;
            Time.timeScale = _timeScale;
            _jobWorkerCount = Mathf.Clamp(_jobWorkerCount, 1, JobsUtility.JobWorkerMaximumCount);
            JobsUtility.JobWorkerCount = _jobWorkerCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateSettings();
        }
#endif
    }
}