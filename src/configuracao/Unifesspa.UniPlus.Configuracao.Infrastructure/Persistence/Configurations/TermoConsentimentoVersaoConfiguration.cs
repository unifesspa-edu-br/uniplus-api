namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

/// <summary>
/// Configuração EF Core da tabela append-only <c>termo_consentimento_versao</c>
/// (UNI-REQ-0086/RN-COL-05). Sem soft-delete, sem audit fields, sem updates —
/// qualquer mutação fora de <c>INSERT</c> é incidente operacional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TermoConsentimentoVersaoConfiguration : IEntityTypeConfiguration<TermoConsentimentoVersao>
{
    private const int HashLength = 64;
    private const int PromovidaPorMaxLength = 255;

    public void Configure(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("termo_consentimento_versao");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TermoConsentimentoId).IsRequired();

        builder.Property(v => v.Texto).IsRequired();
        builder.Property(v => v.BaseLegal).IsRequired();

        builder.Property(v => v.FormaAceite)
            .HasConversion<FormaAceiteValueConverter>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.Hash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(v => v.PromovidaEm).IsRequired();

        builder.Property(v => v.PromovidaPor)
            .HasMaxLength(PromovidaPorMaxLength)
            .IsRequired();

        builder.HasIndex(v => new { v.TermoConsentimentoId, v.PromovidaEm })
            .HasDatabaseName("ix_termo_consentimento_versao_termo_promovida_em");
    }
}
