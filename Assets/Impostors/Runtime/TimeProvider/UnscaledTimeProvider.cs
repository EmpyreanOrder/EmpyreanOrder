namespace Impostors.TimeProvider
{
    public class UnscaledTimeProvider : ITimeProvider
    {
        private float _cachedTime;
        private float _cachedDeltaTime;

        public float Time => _cachedTime;
        public float DeltaTime => _cachedDeltaTime;

        public UnscaledTimeProvider()
        {
            Update();
        }

        public void Update()
        {
            _cachedTime = UnityEngine.Time.unscaledTime;
            _cachedDeltaTime = UnityEngine.Time.unscaledDeltaTime;
        }
    }
}