namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="EstadoIndicador"/> — satélite socioeconômico 1:1 de
/// <see cref="Estado"/>. A relação é configurada como <c>WithOne</c> (1:1 real no
/// modelo EF, não many-to-one com unique), com FK <c>estado_id</c> UNIQUE.
/// </summary>
/// <remarks>
/// <c>receitas_brutas</c>/<c>despesas_brutas</c> chegam a ~bilhões → <c>numeric(18,2)</c>.
/// As demais métricas ficam <c>numeric</c> sem precisão fixa: dado externo volátil
/// e nullable (parse tolerante no ETL, ADR-0092), preservado sem truncamento.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EstadoIndicadorConfiguration : IEntityTypeConfiguration<EstadoIndicador>
{
    public void Configure(EntityTypeBuilder<EstadoIndicador> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("estado_indicador");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ReceitasBrutas).HasPrecision(18, 2);
        builder.Property(i => i.DespesasBrutas).HasPrecision(18, 2);

        // Nomes explícitos com separador antes do ano — o snake_case automático
        // geraria `populacao_residente2022` (ano colado), ambíguo para ETL/SQL.
        builder.Property(i => i.PopulacaoResidente2022).HasColumnName("populacao_residente_2022");
        builder.Property(i => i.MatriculasEnsinoFundamental2023).HasColumnName("matriculas_ensino_fundamental_2023");
        builder.Property(i => i.TotalVeiculos2023).HasColumnName("total_veiculos_2023");

        // 1:1 com Estado: WithOne + FK dedicada. A unicidade de estado_id é o que
        // materializa a cardinalidade no banco; o índice nomeado abaixo a expressa.
        builder.HasOne<Estado>()
            .WithOne()
            .HasForeignKey<EstadoIndicador>(i => i.EstadoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.EstadoId)
            .IsUnique()
            .HasDatabaseName("ix_estado_indicador_estado_id");

        builder.ConfigurarProveniencia(i => i.VersaoDataset, i => i.Vigente);
        builder.HasIndex(i => i.VersaoDataset)
            .HasDatabaseName("ix_estado_indicador_versao_dataset");
    }
}
