namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os corretores que o teste entrega ao congelamento e ao checklist.</summary>
internal static class CorretoresDeTeste
{
    /// <summary>O conjunto de quem não referencia regra de consolidação: só a etapa sem regra tem resposta.</summary>
    public static CorretoresDasEtapas SemConsolidacao { get; } = new(new Dictionary<ReferenciaRegra, int>());
}
