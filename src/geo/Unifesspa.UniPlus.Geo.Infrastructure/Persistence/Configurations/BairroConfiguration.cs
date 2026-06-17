namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Bairro"/>. Chave natural <c>(cidade_id, nome_normalizado)</c>
/// UNIQUE (upsert idempotente do ETL); <c>nome_normalizado</c> é <c>NOT NULL</c> por
/// compor a chave. FK intra-banco para <c>cidade</c> (ADR-0054); coordenada GIST
/// (ADR-0091).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class BairroConfiguration : IEntityTypeConfiguration<Bairro>
{
    public void Configure(EntityTypeBuilder<Bairro> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bairro");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Uf).IsRequired();
        builder.Property(b => b.Nome).IsRequired();
        builder.Property(b => b.NomeNormalizado).IsRequired();
        builder.Property(b => b.Latitude).HasPrecision(9, 6);
        builder.Property(b => b.Longitude).HasPrecision(9, 6);
        builder.ConfigurarCoordenada(b => b.Coordenada);

        builder.HasOne<Cidade>()
            .WithMany()
            .HasForeignKey(b => b.CidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chave natural única (cidade + nome). cidade_id à esquerda cobre a FK.
        builder.HasIndex(b => new { b.CidadeId, b.NomeNormalizado })
            .IsUnique()
            .HasDatabaseName("ix_bairro_cidade_nome");

        builder.HasIndex(b => b.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("ix_bairro_coordenada");

        builder.ConfigurarProveniencia(b => b.VersaoDataset, b => b.Vigente);
        builder.HasIndex(b => b.VersaoDataset)
            .HasDatabaseName("ix_bairro_versao_dataset");
    }
}
