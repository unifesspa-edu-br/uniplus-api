namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os corretores que o teste entrega ao congelamento e ao checklist.</summary>
internal static class CorretoresDeTeste
{
    /// <summary>O conjunto de quem não referencia regra de consolidação: só a etapa sem regra tem resposta.</summary>
    public static CorretoresDasEtapas SemConsolidacao { get; } = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    /// <summary>
    /// O conjunto do processo: a etapa de tipo que exige correção independente tem a regra lida com os dois corretores
    /// que o tipo exige, e as demais seguem sem consolidação.
    /// </summary>
    public static CorretoresDasEtapas DoProcesso(ProcessoSeletivo processo) => new(processo.Etapas
        .Where(static etapa => etapa.TipoEtapa.ExigeCorrecaoIndependente && etapa.RegraConsolidacao is not null)
        .Select(static etapa => etapa.RegraConsolidacao!)
        .Distinct()
        .ToDictionary(static regra => regra, static _ => CorretoresDaRegra.SemNovoCorretor(2)));
}
