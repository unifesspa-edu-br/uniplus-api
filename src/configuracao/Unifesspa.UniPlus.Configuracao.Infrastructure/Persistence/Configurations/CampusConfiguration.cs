namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // CHECK de coerência cidade↔CEP (CA-04, ADR-0096): NULL-safe — ver
        // EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckSql.
        builder.ToTable("campus", t =>
        {
            t.HasCheckConstraint(
                EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckName("campus"),
                EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckSql);
            t.HasCheckConstraint(
                EnderecoGeoOwnedConfiguration.CompletudeCheckName("campus"),
                EnderecoGeoOwnedConfiguration.CompletudeCheckSql);
        });
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

        builder.Property(c => c.CodigoEmec).HasMaxLength(20);

        // Endereço estruturado ao Geo via CEP (ADR-0096): owned type opcional.
        builder.OwnsOne(c => c.Endereco, EnderecoGeoOwnedConfiguration.Configure);
        builder.Navigation(c => c.Endereco).IsRequired(false);

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
