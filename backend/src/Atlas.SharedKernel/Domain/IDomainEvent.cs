namespace Atlas.SharedKernel.Domain;

/// <summary>
/// A fact that has already happened inside a single aggregate.
/// Raised by aggregates, dispatched by infrastructure after the
/// transaction commits - never during it.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable identity, useful for idempotent consumers.</summary>
    Guid EventId { get; }

    /// <summary>When the fact occurred (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Convenience base record for domain events.</summary>
/// <param name="OccurredAt">When the fact occurred (UTC).</param>
public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; } = Guid.CreateVersion7();
}
