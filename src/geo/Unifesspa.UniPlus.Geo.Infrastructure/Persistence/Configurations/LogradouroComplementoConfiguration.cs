namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="LogradouroComplemento"/> — complemento por CEP, sem FK a
/// um logradouro (o complemento é atributo do CEP, não de um logradouro específico).
/// O <c>cep</c> é indexado mas NÃO único; a idempotência vem da chave composta UNIQUE
/// <c>(cep, complemento_normalizado)</c>, cujo prefixo <c>cep</c> também serve o
/// lookup — por isso não há índice standalone de <c>cep</c> (redundante).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LogradouroComplementoConfiguration : IEntityTypeConfiguration<LogradouroComplemento>
{
    public void Configure(EntityTypeBuilder<LogradouroComplemento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("logradouro_complemento");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Cep).IsRequired();
        builder.Property(c => c.Complemento).IsRequired();
        builder.Property(c => c.ComplementoNormalizado).IsRequired();

        builder.HasIndex(c => new { c.Cep, c.ComplementoNormalizado })
            .IsUnique()
            .HasDatabaseName("ux_logradouro_complemento_cep_complemento");

        builder.ConfigurarProveniencia(c => c.VersaoDataset, c => c.Vigente);
        builder.HasIndex(c => c.VersaoDataset)
            .HasDatabaseName("ix_logradouro_complemento_versao_dataset");
    }
}
