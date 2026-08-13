namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DiaNaoUtilConfiguration : IEntityTypeConfiguration<DiaNaoUtil>
{
    private const int DescricaoMaxLength = 200;

    public void Configure(EntityTypeBuilder<DiaNaoUtil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "dia_nao_util",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_dia_nao_util_municipio_coerente",
                    "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) "
                    + "AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) "
                    + "AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)");
                t.HasCheckConstraint(
                    "ck_dia_nao_util_uf_coerente",
                    "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)");
            });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.CalendarioDiasUteisId).IsRequired();

        builder.Property(d => d.Abrangencia)
            .HasConversion<AbrangenciaValueConverter>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.MunicipioIbge).HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength);
        builder.Property(d => d.MunicipioNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength);
        builder.Property(d => d.MunicipioUf).HasMaxLength(ReferenciaCidadeGeo.UfLength);
        builder.Property(d => d.Uf).HasMaxLength(ReferenciaCidadeGeo.UfLength);
        builder.Property(d => d.Data).IsRequired();
        builder.Property(d => d.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();

        builder.HasIndex(d => new { d.CalendarioDiasUteisId, d.Data })
            .HasDatabaseName("ix_dia_nao_util_calendario_data");
    }
}
