namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A suspensão da permissão que o catálogo atual reclassificou para cima ou mudou de escopo, na concessão: o fragmento da
/// consulta de acesso que compara o catálogo atual do banco com a base da permissão na concessão. A base é a última linha
/// gravada para a concessão e o código, desde que não seja de saída; sem ela, é o retrato da versão da concessão. O código
/// que o catálogo atual não conhece não suspende nada. Sem cache entre requisições; quando o cache da ADR-0085 existir,
/// toda conciliação e toda liberação terão de invalidá-lo.
/// </summary>
internal static class SuspensaoPorReclassificacao
{
    /// <summary>Os códigos da composição, sem os que o catálogo atual suspende na concessão. Governança nunca se suspende.</summary>
    public static IQueryable<CodigoDaConcessao> SemSuspensasPorReclassificacao(
        this UniPlusDbContext context, IQueryable<CodigoDaConcessao> codigos) =>
        from k in codigos
        join b in context.BasesVivas() on new { k.ConcessaoId, k.Codigo } equals new { b.ConcessaoId, b.Codigo } into daBase
        from b in daBase.DefaultIfEmpty()
        where !k.DeVersao || !context.ImplantacaoAtual().Any(p => p.Codigo == k.Codigo
            && (FuncoesDoBanco.OrdemDaClassificacao(p.Classificacao)
                    > FuncoesDoBanco.OrdemDaClassificacao(((ClassificacaoDeProtecao?)b.Classificacao ?? k.RetratoDaClassificacao)!.Value)
                || p.DeUnidade != ((bool?)b.DeUnidade ?? k.RetratoDeUnidade)))
        select k;

    /// <summary>As permissões do catálogo de maior sequência.</summary>
    public static IQueryable<PermissaoImplantada> ImplantacaoAtual(this UniPlusDbContext context) =>
        context.PermissoesImplantadas.Where(p =>
            p.ImplantacaoId == context.Set<ImplantacaoDasPermissoes>().OrderByDescending(c => c.Sequencia).Select(c => c.Id).First());

    /// <summary>A última base de cada par (concessão, código), quando não é de saída.</summary>
    public static IQueryable<BaseViva> BasesVivas(this UniPlusDbContext context) =>
        from b in context.BasesDasPermissoes
        where b.Origem != OrigemDaBase.Saida
            && !context.Set<BaseDaPermissaoNaConcessao>().Any(o => o.ConcessaoId == b.ConcessaoId && o.Codigo == b.Codigo && o.OrdemNaTrilha > b.OrdemNaTrilha)
        select new BaseViva { ConcessaoId = b.ConcessaoId, Codigo = b.Codigo, Classificacao = b.Classificacao!.Value, DeUnidade = b.DeUnidade!.Value };
}

/// <summary>A base em vigor de uma permissão numa concessão.</summary>
internal sealed class BaseViva
{
    public Guid ConcessaoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public ClassificacaoDeProtecao Classificacao { get; init; }

    public bool DeUnidade { get; init; }
}
