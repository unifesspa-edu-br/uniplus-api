namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("campus");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Sigla).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Nome).HasMaxLength(200).IsRequired();

        // Referência de cidade do Geo (ADR-0090): código + display cache, sem FK
        // cross-banco para uniplus_geo.
        builder.Property(c => c.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(c => c.CidadeNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength).IsRequired();
        builder.Property(c => c.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(c => c.CidadeOrigem).HasMaxLength(ReferenciaCidadeGeo.OrigemMaxLength);
        builder.Property(c => c.CidadeDisplayAtualizadoEm);

        builder.Property(c => c.Endereco).HasMaxLength(500);
        builder.Property(c => c.Cep).HasMaxLength(8).IsFixedLength();
        builder.Property(c => c.Latitude).HasPrecision(9, 6);
        builder.Property(c => c.Longitude).HasPrecision(9, 6);
        builder.Property(c => c.CodigoEmec).HasMaxLength(20);

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Unicidade da sigla entre campi vivos (índice parcial).
        builder.HasIndex(c => c.Sigla)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_campus_sigla_vivo");

        // Índice de relatório/filtro pela cidade (ADR-0090).
        builder.HasIndex(c => c.CidadeCodigoIbge)
            .HasDatabaseName("ix_campus_cidade_codigo_ibge");
    }
}
