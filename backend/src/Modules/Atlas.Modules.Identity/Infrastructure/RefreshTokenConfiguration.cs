using Atlas.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Modules.Identity.Infrastructure;

/// <summary>Maps <see cref="RefreshToken"/> onto <c>identity.refresh_tokens</c>.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashLength)
            .IsRequired();

        builder.Property(token => token.UserId).IsRequired();
        builder.Property(token => token.FamilyId).IsRequired();
        builder.Property(token => token.IssuedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();

        // Every refresh is a lookup by hash, so this index is on the hot path
        // as well as being the uniqueness guarantee.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token_hash");

        // Revoking a family on reuse detection scans by family.
        builder.HasIndex(token => token.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        // Supports both "all sessions for a user" and expiry cleanup.
        builder.HasIndex(token => new { token.UserId, token.ExpiresAt })
            .HasDatabaseName("ix_refresh_tokens_user_id_expires_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
