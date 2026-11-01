namespace TaskIsland.Core.Services;

/// <summary>Time source, replaceable in tests.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}
