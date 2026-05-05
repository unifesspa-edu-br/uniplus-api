namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_cache");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Scope)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Endpoint)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.BodyHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(e => e.ResponseHeadersJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Índice único — garante que duas requests concorrentes com a mesma
        // (scope, endpoint, key) não consigam reservar duas entries.
        // DbUpdateException por violação dessa unique é o sinal de retry
        // concorrente (tratado pelo filter com aguarda + replay).
        builder.HasIndex(e => new { e.Scope, e.Endpoint, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("idx_idempotency_lookup");

        // Suporta job de cleanup por TTL (não implementado nesta story; ADR-0027
        // §"Esta ADR não decide" — política de cleanup fica aberta).
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("idx_idempotency_expires_at");
    }
}
