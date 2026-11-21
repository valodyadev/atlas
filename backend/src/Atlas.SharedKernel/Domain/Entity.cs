namespace Atlas.SharedKernel.Domain;

/// <summary>
/// An object with a stable identity whose equality is identity-based
/// rather than value-based.
/// </summary>
/// <typeparam name="TId">The identity type.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Creates an entity with the given identity.</summary>
    protected Entity(TId id) => Id = id;

    /// <summary>EF Core materialisation constructor.</summary>
    protected Entity() => Id = default!;

    /// <summary>The entity identity.</summary>
    public TId Id { get; protected init; }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && Id.Equals(other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// The transactional boundary. Only aggregate roots are loaded and saved as a
/// unit, and only they may raise domain events.
/// </summary>
/// <typeparam name="TId">The identity type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Creates an aggregate with the given identity.</summary>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>EF Core materialisation constructor.</summary>
    protected AggregateRoot() { }

    /// <summary>Events raised since the aggregate was loaded.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Records a fact about this aggregate.</summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by infrastructure once events have been dispatched.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
