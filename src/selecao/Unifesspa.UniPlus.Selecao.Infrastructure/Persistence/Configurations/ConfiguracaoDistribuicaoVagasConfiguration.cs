namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoDistribuicaoVagas"/> (Story
/// #773) — entidade filha do agregado <see cref="ProcessoSeletivo"/>, uma por
/// oferta de curso.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoDistribuicaoVagasConfiguration : IEntityTypeConfiguration<ConfiguracaoDistribuicaoVagas>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

    // Alinhado ao ReferenciaReservaDemograficaConfiguration (Configuracao) —
    // o snapshot-copy (ADR-0061) precisa caber qualquer valor aceito na
    // origem, senão um Censo válido lá estoura em SaveChanges aqui em vez de
    // persistir (achado Codex).
    private const int CensoMaxLength = 20;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_distribuicao_vagas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.VoBase).IsRequired();
        builder.Property(c => c.Pr).HasPrecision(5, 4).IsRequired();

        // ReferenciaRegra (VO do rol_de_regras, Story #772) embutida por valor —
        // a regra de distribuição aplicada (codigo+versao+hash), congelada no
        // momento em que o admin configurou. EF materializa via o construtor
        // privado (constructor binding), já que o VO não expõe setters.
        builder.OwnsOne(c => c.RegraDistribuicao, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_distribuicao_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(r => r.Versao).HasColumnName("regra_distribuicao_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(r => r.Hash).HasColumnName("regra_distribuicao_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(c => c.RegraDistribuicao).IsRequired();

        // ReferenciaReservaDemograficaSnapshot — opcional (só na Lei 12.711,
        // INV-5). Colunas nulas quando a distribuição é institucional.
        builder.OwnsOne(c => c.ReferenciaDemografica, demografica =>
        {
            demografica.Property(d => d.OrigemId).HasColumnName("referencia_demografica_origem_id");
            demografica.Property(d => d.CensoReferencia).HasColumnName("referencia_demografica_censo").HasMaxLength(CensoMaxLength);
            demografica.Property(d => d.PpiPercentual).HasColumnName("referencia_demografica_ppi_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.QuilombolaPercentual).HasColumnName("referencia_demografica_quilombola_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.PcdPercentual).HasColumnName("referencia_demografica_pcd_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.BaseLegal).HasColumnName("referencia_demografica_base_legal").HasMaxLength(BaseLegalMaxLength);
        });
        builder.Navigation(c => c.ReferenciaDemografica).IsRequired(false);

        // RegraAjuste (issue #848/ADR-0115) — obrigatória no ramo federal, opcional
        // no institucional (quadro fixo não reconcilia).
        builder.OwnsOne(c => c.RegraAjuste, regraAjuste =>
        {
            regraAjuste.Property(r => r.Codigo).HasColumnName("regra_ajuste_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regraAjuste.Property(r => r.Versao).HasColumnName("regra_ajuste_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regraAjuste.Property(r => r.Hash).HasColumnName("regra_ajuste_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(c => c.RegraAjuste).IsRequired(false);

        // Coleção filha: entidade própria com FK para a raiz (nunca owned types).
        builder.HasMany(c => c.Modalidades)
            .WithOne()
            .HasForeignKey(m => m.ConfiguracaoDistribuicaoVagasId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Modalidades)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // O quadro de vagas (issue #848/ADR-0115) — output derivado, materializado
        // dentro da mesma factory que os insumos (ADR-0115), nunca por comando
        // separado. Mesma forma de mapeamento das Modalidades: entidade própria,
        // FK para a raiz, substituída por inteiro a cada redefinição.
        builder.HasMany(c => c.VagasOfertadas)
            .WithOne()
            .HasForeignKey(v => v.ConfiguracaoDistribuicaoVagasId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.VagasOfertadas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // UNIQUE parcial: uma configuração de distribuição por oferta de curso
        // dentro do mesmo processo (INV do agregado, ProcessoSeletivo.OfertaCursoDuplicada).
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.OfertaCursoOrigemId })
            .IsUnique()
            .HasDatabaseName("ux_configuracoes_distribuicao_vagas_processo_oferta");
    }
}
