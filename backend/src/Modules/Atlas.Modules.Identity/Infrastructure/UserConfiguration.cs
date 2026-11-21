using Atlas.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Modules.Identity.Infrastructure;

/// <summary>Maps <see cref="User"/> onto the <c>identity.users</c> table.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(User.EmailMaxLength)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        // Enforced in the database, not just in application code: concurrent
        // requests would otherwise both pass an application-level check.
        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        // Domain events are transient; they are dispatched after commit and
        // never persisted by this context.
        builder.Ignore(user => user.DomainEvents);
    }
}
