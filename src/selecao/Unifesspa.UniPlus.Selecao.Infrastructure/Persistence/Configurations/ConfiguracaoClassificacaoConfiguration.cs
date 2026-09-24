namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoClassificacao"/> (Story
/// #775) — o 15º bloco canônico, 1:1 do agregado
/// <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoClassificacaoConfiguration : IEntityTypeConfiguration<ConfiguracaoClassificacao>
{

    public void Configure(EntityTypeBuilder<ConfiguracaoClassificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_classificacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.RegraCalculo, regra => regra.ConfigurarReferenciaRegra("regra_calculo"));
        builder.Navigation(c => c.RegraCalculo).IsRequired();

        // Opcional (INV-B8): ausente quando RegraCalculo é CLASSIFICACAO-IMPORTADA.
        builder.OwnsOne(c => c.RegraArredondamento, regra => regra.ConfigurarReferenciaRegra("regra_arredondamento"));
        builder.Navigation(c => c.RegraArredondamento).IsRequired(false);

        builder.Property(c => c.CasasArredondamento);

        builder.OwnsOne(c => c.RegraOrdemAlocacao, regra => regra.ConfigurarReferenciaRegra("regra_ordem_alocacao"));
        builder.Navigation(c => c.RegraOrdemAlocacao).IsRequired();

        builder.Property(c => c.NOpcoesAlocacao).IsRequired();

        builder.Property(c => c.BaseadoEmEnem)
            .HasColumnName("baseado_em_enem")
            .IsRequired()
            .HasComment(
                "A classificação usa a estrutura de pontuação por área do ENEM — sinal " +
                "explícito (Story #850) do qual as regras de eliminação do ENEM dependem, " +
                "substituindo a ramificação por TipoProcesso.");

        // Coleção filha: entidade própria com FK para a raiz (nunca owned types).
        builder.HasMany(c => c.RegrasEliminacao)
            .WithOne()
            .HasForeignKey(r => r.ConfiguracaoClassificacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.RegrasEliminacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(c => c.ResolucaoPesoAreaEnem)
            .HasColumnName("resolucao_peso_area_enem")
            .HasMaxLength(LimitesDoEnvelope.ResolucaoPesoAreaEnem)
            .HasComment(
                "Resolução de Pesos por Área declarada pela classificação baseada em ENEM com cálculo local; " +
                "o vínculo com o cadastro é pelo valor, e o quadro fica congelado em grupos_peso_area_enem_congelados. " +
                "Nulo nas demais classificações.");

        builder.HasMany(c => c.QuadroPesoAreaEnem)
            .WithOne()
            .HasForeignKey(g => g.ConfiguracaoClassificacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.QuadroPesoAreaEnem)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
