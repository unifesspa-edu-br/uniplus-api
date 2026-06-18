namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento do registro de execução do ETL DNE (Story #674). O destaque é o índice
/// único parcial que garante <strong>no máximo uma</strong> execução em andamento
/// (concorrência → 409): UNIQUE sobre <c>status</c> filtrado por <c>status = 0</c>
/// (<see cref="StatusImportacao.EmAndamento"/>) — por isso o enum não pode ser reordenado.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflexão.")]
internal sealed class GeoImportacaoExecucaoConfiguration : IEntityTypeConfiguration<GeoImportacaoExecucao>
{
    public void Configure(EntityTypeBuilder<GeoImportacaoExecucao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("geo_importacao_execucao");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.VersaoDataset).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.IniciadoEm).IsRequired();
        builder.Property(e => e.DisparadoPor).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RelatorioJson).HasColumnType("jsonb");
        builder.Property(e => e.Mensagem).HasMaxLength(1000);

        // Concorrência (CA-06): no máximo uma execução em andamento. UNIQUE parcial sobre
        // status filtrado por status = 0 (EmAndamento) — entre as linhas EmAndamento o valor
        // de status é constante (0), então só uma pode existir. O segundo disparo colide na
        // UNIQUE (23505) e vira 409. O valor 0 do enum é parte do contrato (não reordenar).
        builder.HasIndex(e => e.Status)
            .IsUnique()
            .HasFilter("status = 0")
            .HasDatabaseName("ux_geo_importacao_execucao_em_andamento");

        builder.HasIndex(e => e.IniciadoEm)
            .HasDatabaseName("ix_geo_importacao_execucao_iniciado_em");
    }
}
