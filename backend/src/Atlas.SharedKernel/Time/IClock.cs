namespace Atlas.SharedKernel.Time;

/// <summary>
/// Time as a dependency. Domain code never reads <c>DateTimeOffset.UtcNow</c>
/// directly, which is what makes time-dependent behaviour testable.
/// </summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Clock backed by the system time source.</summary>
/// <param name="timeProvider">The underlying .NET time provider.</param>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
