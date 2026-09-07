namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
            args.Property(x => x.SuspensividadePrimeiraInstanciaValor).HasColumnName("suspensividade_1a_instancia_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadePrimeiraInstanciaUnidade).HasColumnName("suspensividade_1a_instancia_unidade").HasConversion<int>();
            args.Property(x => x.SuspensividadeSegundaInstanciaValor).HasColumnName("suspensividade_2a_instancia_valor").HasPrecision(18, 4);
            args.Property(x => x.SuspensividadeSegundaInstanciaUnidade).HasColumnName("suspensividade_2a_instancia_unidade").HasConversion<int>();
        });
        builder.Navigation(r => r.Args).IsRequired();

        // Coluna crua, sem FK para produtos_da_fase, ao contrário do que DocumentoExigido faz
        // com fases_cronograma. A reconciliação de FaseCronograma.AtualizarSnapshot esvazia e
        // repõe a coleção de produtos, e uma FK obrigatória faz o EF ler a saída do produto
        // âncora da coleção como rompimento de relação obrigatória — o SaveChanges estoura
        // antes de a âncora ser remapeada, ainda que o estado final seja coerente. A
        // referência é interna ao agregado e nunca atravessa fase: quem a mantém coerente é
        // FaseCronograma, que valida a âncora ao criar e a remapeia ao reconciliar.
        builder.Property(r => r.ProdutoAncoraId).HasColumnName("produto_ancora_id").IsRequired();

        // 0..1 por fase — a FK É a chave alternativa que garante a cardinalidade.
        builder.HasIndex(r => r.FaseCronogramaId)
            .IsUnique()
            .HasDatabaseName("ux_regras_recurso_fase_fase_cronograma");
    }
}
