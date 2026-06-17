namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Distrito"/>. Sem código IBGE: a chave natural é
/// <c>(cidade_id, nome_normalizado)</c> UNIQUE — base do upsert idempotente do ETL.
/// <c>nome_normalizado</c> é <c>NOT NULL</c> (compõe a chave; UNIQUE com nulo
/// admitiria duplicatas em PostgreSQL). FK intra-banco para <c>cidade</c>
/// (ADR-0054); coordenada GIST (ADR-0091).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DistritoConfiguration : IEntityTypeConfiguration<Distrito>
{
    public void Configure(EntityTypeBuilder<Distrito> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("distrito");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Uf).IsRequired();
        builder.Property(d => d.Nome).IsRequired();
        builder.Property(d => d.NomeNormalizado).IsRequired();
        builder.Property(d => d.Latitude).HasPrecision(9, 6);
        builder.Property(d => d.Longitude).HasPrecision(9, 6);
        builder.ConfigurarCoordenada(d => d.Coordenada);

        builder.HasOne<Cidade>()
            .WithMany()
            .HasForeignKey(d => d.CidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chave natural única (cidade + nome). cidade_id à esquerda cobre a FK.
        builder.HasIndex(d => new { d.CidadeId, d.NomeNormalizado })
            .IsUnique()
            .HasDatabaseName("ix_distrito_cidade_nome");

        builder.HasIndex(d => d.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("ix_distrito_coordenada");

        builder.ConfigurarProveniencia(d => d.VersaoDataset, d => d.Vigente);
        builder.HasIndex(d => d.VersaoDataset)
            .HasDatabaseName("ix_distrito_versao_dataset");
    }
}
