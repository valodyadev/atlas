using Atlas.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Atlas.Modules.Identity.Tests;

/// <summary>
/// Guards the keyset-pagination query against silently degrading into
/// client-side evaluation or a wrong ordering.
/// </summary>
/// <remarks>
/// This is the failure mode worth a dedicated test: if the cursor comparison
/// stops translating to a server-side <c>uuid</c> comparison, the endpoint
/// keeps returning HTTP 200 with quietly wrong pages. EF can produce the SQL
/// without a live connection, so this runs in CI with no database.
/// </remarks>
public sealed class KeysetPaginationTranslationTests
{
    [Fact]
    public void Cursor_comparison_is_translated_to_server_side_sql()
    {
        using var dbContext = new IdentityDbContextFactory().CreateDbContext([]);
        var cursor = Guid.CreateVersion7();

        var sql = dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id.CompareTo(cursor) < 0)
            .OrderByDescending(user => user.Id)
            .Take(51)
            .ToQueryString();

        // A server-side comparison and ordering on the uuid primary key...
        sql.ShouldContain("identity.users");
        sql.ShouldContain("ORDER BY");
        sql.ShouldContain("DESC");
        sql.ShouldContain("LIMIT");
        sql.ShouldContain("u.id <");

        // ...and no evidence that EF gave up and pulled rows into memory.
        sql.ShouldNotContain("CompareTo");
    }
}
