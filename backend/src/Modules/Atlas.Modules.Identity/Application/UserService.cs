using Atlas.Modules.Identity.Contracts;
using Atlas.Modules.Identity.Domain;
using Atlas.Modules.Identity.Infrastructure;
using Atlas.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.Identity.Application;

/// <summary>
/// Use cases for the Identity module.
/// </summary>
/// <remarks>
/// A plain service is enough at this size. When a module outgrows one service
/// class, split it into one handler per use case rather than letting this
/// class grow - the module boundary stays the same either way.
/// </remarks>
/// <param name="dbContext">Module persistence.</param>
public sealed class UserService(IdentityDbContext dbContext)
{
    /// <summary>Lists users, newest first, using keyset pagination.</summary>
    /// <param name="page">Page size and cursor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of users.</returns>
    public async Task<Page<UserResponse>> ListAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        // The UUIDv7 primary key doubles as the paging cursor. Postgres
        // compares `uuid` byte-wise, which is a stable total order, and the
        // 48-bit millisecond prefix makes that order broadly chronological.
        // No OFFSET, so page 10,000 costs the same as page 1.
        //
        // Caveat worth knowing: within a single millisecond the remaining bits
        // are random, so same-millisecond rows are ordered arbitrarily
        // relative to each other. Paging stays correct (no duplicates, no
        // gaps); only the order inside one millisecond is unspecified. If
        // strict creation order ever matters, page on (created_at, id).
        if (Guid.TryParse(page.Cursor, out var cursor))
        {
            query = query.Where(user => user.Id.CompareTo(cursor) < 0);
        }

        var users = await query
            .OrderByDescending(user => user.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = users.Count > page.Limit;
        if (hasMore)
        {
            users.RemoveAt(users.Count - 1);
        }

        return new Page<UserResponse>(
            users.ConvertAll(ToResponse),
            hasMore ? users[^1].Id.ToString() : null);
    }

    /// <summary>Finds a user by identity.</summary>
    /// <param name="id">The user identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or a not-found error.</returns>
    public async Task<Result<UserResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return user is null
            ? Error.NotFound("identity.user.not_found", $"No user with id '{id}'.")
            : ToResponse(user);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.CreatedAt);
}
