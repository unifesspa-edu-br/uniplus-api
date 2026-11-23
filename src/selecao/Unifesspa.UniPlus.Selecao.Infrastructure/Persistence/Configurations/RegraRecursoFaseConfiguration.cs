namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="RegraRecursoFase"/> (Story #851) — filha 0..1 de
/// <see cref="FaseCronograma"/>. <see cref="RegraRecursoFase.Parametros"/> não é polimórfico
/// (única variante, <c>ParametrosRegraPrazoRecurso</c>) — mapeado como owned type de colunas
/// normais, no mesmo molde de <c>ReferenciaRegra</c>, não como JSON.
/// </summary>
public sealed class RegraRecursoFaseConfiguration : IEntityTypeConfiguration<RegraRecursoFase>
{

    public void Configure(EntityTypeBuilder<RegraRecursoFase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("recursos_configurados_da_fase", tabela => tabela.HasComment("Configuração dos recursos de uma fase do cronograma."));
        builder.Property(r => r.FaseCronogramaId).HasComment("Identificador do registro ao qual esta configuração pertence.");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasComment("Identificador do registro.").ValueGeneratedNever();

        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.ConfigurarReferenciaRegra("regra");
            regra.Property(x => x.Codigo).HasComment("Código da regra aplicada ao prazo de recurso.");
            regra.Property(x => x.Versao).HasComment("Versão da regra aplicada ao prazo de recurso.");
            regra.Property(x => x.Hash).HasComment("Resumo do conteúdo da regra aplicada ao prazo de recurso.");
        });
        builder.Navigation(r => r.Regra).IsRequired();

        builder.OwnsOne(r => r.Parametros, parametros => parametros.ConfigurarParametrosDoRecurso());
        builder.Navigation(r => r.Parametros).IsRequired();

        // Coluna crua, sem FK para publicacoes_previstas_da_fase, ao contrário do que DocumentoExigido faz
        // com fases_cronograma. A reconciliação de FaseCronograma.AtualizarDe esvazia e
        // repõe a coleção de produtos, e uma FK obrigatória faz o EF ler a saída do produto
        // âncora da coleção como rompimento de relação obrigatória — o SaveChanges estoura
        // antes de a âncora ser remapeada, ainda que o estado final seja coerente. A
        // referência é interna ao agregado e nunca atravessa fase: quem a mantém coerente é
        // FaseCronograma, que valida a âncora ao criar e a remapeia ao reconciliar.
        builder.Property(r => r.ProdutoAncoraId).HasComment("Identificador da publicação usada como referência para o início do prazo.").HasColumnName("publicacao_de_referencia_id").IsRequired();
        builder.MapearProcedimentosDeRecurso(r => r.Procedimentos);

        // 0..1 por fase — a FK É a chave alternativa que garante a cardinalidade.
        builder.HasIndex(r => r.FaseCronogramaId)
            .IsUnique()
            .HasDatabaseName("ux_recursos_configurados_da_fase_fase_cronograma");
    }
}
