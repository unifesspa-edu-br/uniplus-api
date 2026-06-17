namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="CidadeIndicador"/> — satélite socioeconômico 1:1 de
/// <see cref="Cidade"/> (<c>WithOne</c>, FK <c>cidade_id</c> UNIQUE).
/// <c>receitas</c>/<c>despesas</c> ~bilhões → <c>numeric(18,2)</c>; demais métricas
/// <c>numeric</c> sem precisão fixa (dado externo volátil/nullable, parse tolerante).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CidadeIndicadorConfiguration : IEntityTypeConfiguration<CidadeIndicador>
{
    public void Configure(EntityTypeBuilder<CidadeIndicador> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cidade_indicador");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Receitas).HasPrecision(18, 2);
        builder.Property(i => i.Despesas).HasPrecision(18, 2);

        builder.HasOne<Cidade>()
            .WithOne()
            .HasForeignKey<CidadeIndicador>(i => i.CidadeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.CidadeId)
            .IsUnique()
            .HasDatabaseName("ix_cidade_indicador_cidade_id");

        builder.ConfigurarProveniencia(i => i.VersaoDataset, i => i.Vigente);
        builder.HasIndex(i => i.VersaoDataset)
            .HasDatabaseName("ix_cidade_indicador_versao_dataset");
    }
}
