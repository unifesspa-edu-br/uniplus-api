namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoClassificacao"/> (Story
/// #775, modelagem P-B §2.1) — o 15º bloco canônico, 1:1 do agregado
/// <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoClassificacaoConfiguration : IEntityTypeConfiguration<ConfiguracaoClassificacao>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<ConfiguracaoClassificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_classificacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.RegraCalculo, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_calculo_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(r => r.Versao).HasColumnName("regra_calculo_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(r => r.Hash).HasColumnName("regra_calculo_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(c => c.RegraCalculo).IsRequired();

        // Opcional (INV-B8): ausente quando RegraCalculo é CLASSIFICACAO-IMPORTADA.
        builder.OwnsOne(c => c.RegraArredondamento, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_arredondamento_codigo").HasMaxLength(RegraCodigoMaxLength);
            regra.Property(r => r.Versao).HasColumnName("regra_arredondamento_versao").HasMaxLength(RegraVersaoMaxLength);
            regra.Property(r => r.Hash).HasColumnName("regra_arredondamento_hash").HasMaxLength(HashLength).IsFixedLength();
        });
        builder.Navigation(c => c.RegraArredondamento).IsRequired(false);

        builder.Property(c => c.CasasArredondamento);

        builder.OwnsOne(c => c.RegraOrdemAlocacao, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_ordem_alocacao_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(r => r.Versao).HasColumnName("regra_ordem_alocacao_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(r => r.Hash).HasColumnName("regra_ordem_alocacao_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(c => c.RegraOrdemAlocacao).IsRequired();

        builder.Property(c => c.NOpcoesAlocacao).IsRequired();

        // Coleção filha: entidade própria com FK para a raiz (nunca owned types).
        builder.HasMany(c => c.RegrasEliminacao)
            .WithOne()
            .HasForeignKey(r => r.ConfiguracaoClassificacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.RegrasEliminacao)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
