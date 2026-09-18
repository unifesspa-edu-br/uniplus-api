namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoCascataRemanejamento"/>
/// (Story #575) — entidade 0..1 do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoCascataRemanejamentoConfiguration : IEntityTypeConfiguration<ConfiguracaoCascataRemanejamento>
{

    public void Configure(EntityTypeBuilder<ConfiguracaoCascataRemanejamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_cascata_remanejamento");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.Regra, regra => regra.ConfigurarReferenciaRegra("regra"));
        builder.Navigation(c => c.Regra).IsRequired();

        builder.Property(c => c.FallbackCodigo).HasMaxLength(ConfiguracaoCascataRemanejamento.FallbackMaxLength).IsRequired();

        // Coleção filha: entidade própria com FK para a raiz (nunca owned types) —
        // mesmo padrão de ConfiguracaoDistribuicaoVagas.Modalidades.
        builder.HasMany(c => c.Destinos)
            .WithOne()
            .HasForeignKey(d => d.ConfiguracaoCascataRemanejamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Destinos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Uma cascata por processo (§2.2 da story — a cascata NÃO é por oferta).
        builder.HasIndex(c => c.ProcessoSeletivoId)
            .IsUnique()
            .HasDatabaseName("ux_configuracoes_cascata_remanejamento_processo");
    }
}
