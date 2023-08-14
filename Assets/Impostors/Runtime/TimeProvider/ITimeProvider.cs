namespace Impostors.TimeProvider
{
    public interface ITimeProvider
    {
        float Time { get; }
        float DeltaTime { get; }

        void Update();
    }
}