namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A recusa de uma configuração que cita uma etapa que o processo não tem, com o mesmo texto na gravação e na reidratação.</summary>
internal static class ReferenciaAEtapaInexistente
{
    internal static DomainError PeloDesempate(int ordemDoCriterio, Guid etapaRef) => new(
        "ProcessoSeletivo.EtapaRefDesempateInexistente",
        $"O critério de desempate na ordem {ordemDoCriterio} referencia a etapa {etapaRef}, que não existe neste processo.");

    internal static DomainError PelaEliminacao(Guid etapaRef) => new(
        "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
        $"A regra de eliminação referencia a etapa {etapaRef}, que não existe neste processo.");

    /// <summary>A recusa do critério de desempate do documento que cita uma etapa inexistente, pela posição (a partir de 1) na lista enviada.</summary>
    internal static DomainError PeloDesempateNaPosicao(int posicao) => new(
        "ProcessoSeletivo.EtapaRefDesempateInexistente",
        $"O critério de desempate na posição {posicao} cita uma etapa que não existe neste processo.");

    /// <summary>A recusa da regra de eliminação do documento que cita uma etapa inexistente, pela posição (a partir de 1) na lista enviada.</summary>
    internal static DomainError PelaEliminacaoNaPosicao(int posicao) => new(
        "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
        $"A regra de eliminação na posição {posicao} cita uma etapa que não existe neste processo.");
}
