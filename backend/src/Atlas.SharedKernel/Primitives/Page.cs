namespace Atlas.SharedKernel.Primitives;

/// <summary>
/// A page of results. Keyset ("seek") pagination is preferred over OFFSET for
/// large tables; this shape supports both by exposing a continuation cursor.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="Items">The items in this page.</param>
/// <param name="NextCursor">Opaque cursor for the next page, or <see langword="null"/> at the end.</param>
public sealed record Page<TItem>(IReadOnlyList<TItem> Items, string? NextCursor)
{
    /// <summary>An empty page.</summary>
    public static Page<TItem> Empty { get; } = new([], null);
}

/// <summary>Request for a page of results.</summary>
/// <param name="Limit">Maximum number of items to return.</param>
/// <param name="Cursor">Opaque cursor returned by a previous page.</param>
public readonly record struct PageRequest(int Limit, string? Cursor)
{
    /// <summary>Largest page we are willing to serve.</summary>
    public const int MaxLimit = 200;

    /// <summary>Page size used when a caller does not ask for one.</summary>
    public const int DefaultLimit = 50;

    /// <summary>Clamps a caller-supplied limit into the allowed range.</summary>
    public static PageRequest Of(int? limit, string? cursor) =>
        new(Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit), cursor);
}
