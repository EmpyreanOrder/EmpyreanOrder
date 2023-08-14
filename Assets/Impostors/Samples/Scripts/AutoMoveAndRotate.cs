using System;
using UnityEngine;

namespace Impostors.Samples
{
    [AddComponentMenu("")]
    internal class AutoMoveAndRotate : MonoBehaviour
    {
        public Vector3andSpace moveUnitsPerSecond = default;
        public Vector3andSpace rotateDegreesPerSecond = default;
        public bool ignoreTimescale = default;
        private float m_LastRealTime;
        private Transform _transform;

        private void Start()
        {
            m_LastRealTime = Time.realtimeSinceStartup;
            _transform = transform;
        }


        // Update is called once per frame
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (ignoreTimescale)
            {
                deltaTime = (Time.realtimeSinceStartup - m_LastRealTime);
                m_LastRealTime = Time.realtimeSinceStartup;
            }
            _transform.Translate(moveUnitsPerSecond.value*deltaTime, moveUnitsPerSecond.space);
            _transform.Rotate(rotateDegreesPerSecond.value*deltaTime, moveUnitsPerSecond.space);
        }


        [Serializable]
        public class Vector3andSpace
        {
            public Vector3 value = default;
            public Space space = Space.Self;
        }
    }
}
