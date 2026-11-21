namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class DiaNaoUtilConfiguration : IEntityTypeConfiguration<DiaNaoUtil>
{
    private const int DescricaoMaxLength = 200;

    public void Configure(EntityTypeBuilder<DiaNaoUtil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("dias_nao_uteis", ConfigurarChecks);
        builder.HasKey(d => d.Id);
        ConfigurarAbrangencia(builder);
        ConfigurarDiaEDescricao(builder);
        builder.HasIndex(d => new { d.CalendarioDiasUteisId, d.Data })
            .HasDatabaseName("ix_dias_nao_uteis_calendario_data");
    }

    private static void ConfigurarAbrangencia(EntityTypeBuilder<DiaNaoUtil> builder)
    {
        builder.Property(d => d.Abrangencia)
            .HasConversion<AbrangenciaValueConverter>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.MunicipioIbge).HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength);
        builder.Property(d => d.MunicipioNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength);
        builder.Property(d => d.MunicipioUf).HasMaxLength(ReferenciaCidadeGeo.UfLength);
        builder.Property(d => d.Uf).HasMaxLength(ReferenciaCidadeGeo.UfLength);
    }

    private static void ConfigurarDiaEDescricao(EntityTypeBuilder<DiaNaoUtil> builder)
    {
        builder.Property(d => d.CalendarioDiasUteisId).IsRequired();
        builder.Property(d => d.Data).IsRequired();
        builder.Property(d => d.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();
    }

    private static void ConfigurarChecks(TableBuilder<DiaNaoUtil> table)
    {
        table.HasCheckConstraint(
            "ck_dias_nao_uteis_municipio_coerente",
            "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) "
            + "AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) "
            + "AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)");
        table.HasCheckConstraint(
            "ck_dias_nao_uteis_uf_coerente",
            "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)");
    }
}
