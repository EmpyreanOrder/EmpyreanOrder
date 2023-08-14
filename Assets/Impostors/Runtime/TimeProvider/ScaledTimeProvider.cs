namespace Impostors.TimeProvider
{
    public class ScaledTimeProvider : ITimeProvider
    {
        private float _cachedTime;
        private float _cachedDeltaTime;

        public float Time => _cachedTime;
        public float DeltaTime => _cachedDeltaTime;

        public ScaledTimeProvider()
        {
            Update();
        }

        public void Update()
        {
            _cachedTime = UnityEngine.Time.time;
            _cachedDeltaTime = UnityEngine.Time.deltaTime;
        }
    }
}