namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="RegraRecursoFase"/> (Story #851) — filha 0..1 de
/// <see cref="FaseCronograma"/>. <see cref="RegraRecursoFase.Args"/> não é polimórfico
/// (única variante, <c>ArgsRegraPrazoRecurso</c>) — mapeado como owned type de colunas
/// normais, no mesmo molde de <c>ReferenciaRegra</c>, não como JSON.
/// </summary>
public sealed class RegraRecursoFaseConfiguration : IEntityTypeConfiguration<RegraRecursoFase>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;
    private const int TipoAtoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<RegraRecursoFase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("regras_recurso_fase");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.Property(x => x.Codigo).HasColumnName("regra_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(x => x.Versao).HasColumnName("regra_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(x => x.Hash).HasColumnName("regra_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(r => r.Regra).IsRequired();

        builder.OwnsOne(r => r.Args, args =>
        {
            args.Property(x => x.PrazoValor).HasColumnName("prazo_valor").HasPrecision(18, 4).IsRequired();
            args.Property(x => x.PrazoUnidade).HasColumnName("prazo_unidade").HasConversion<int>().IsRequired();
            args.Property(x => x.AtoAncoraCodigo).HasColumnName("ato_ancora_codigo").HasMaxLength(TipoAtoCodigoMaxLength).IsRequired();
            args.Property(x => x.SuspensividadePrimeiraInstanciaValor).HasColumnName("suspensividade_1a_instancia_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadePrimeiraInstanciaUnidade).HasColumnName("suspensividade_1a_instancia_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadeSegundaInstanciaValor).HasColumnName("suspensividade_2a_instancia_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadeSegundaInstanciaUnidade).HasColumnName("suspensividade_2a_instancia_unidade").HasConversion<int>();
        });
        builder.Navigation(r => r.Args).IsRequired();

        // 0..1 por fase — a FK É a chave alternativa que garante a cardinalidade.
        builder.HasIndex(r => r.FaseCronogramaId)
            .IsUnique()
            .HasDatabaseName("ux_regras_recurso_fase_fase_cronograma");
    }
}
