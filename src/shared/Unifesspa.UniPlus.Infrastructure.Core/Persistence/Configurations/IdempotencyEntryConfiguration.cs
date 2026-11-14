namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("idempotency_cache");

        builder.HasKey(e => e.Id);

        // O escopo da chave de idempotência é user:{sub}: identifica quem agiu.
        builder.Property(e => e.Scope)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Endpoint)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(255)
            .IsRequired();

        // SHA-256 sem sal de um corpo que pode trazer CPF: dá para confirmar um palpite, então vale a classe do corpo.
        builder.Property(e => e.BodyHash)
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<short>()
            .IsRequired();

        // O corpo e os cabeçalhos cacheados são a resposta de qualquer endpoint idempotente e podem trazer dado de qualquer classe.
        builder.Property(e => e.EncryptedResponseBody)
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel);

        builder.Property(e => e.ResponseHeadersJson)
            .ClassificaComo(ClassificacaoDeProtecao.Sensivel)
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
            .HasDatabaseName("ux_idempotency_cache_scope_endpoint_key");

        // Suporta job de cleanup por TTL (não implementado nesta story; ADR-0027
        // §"Esta ADR não decide" — política de cleanup fica aberta).
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_idempotency_cache_expires_at");
    }
}
