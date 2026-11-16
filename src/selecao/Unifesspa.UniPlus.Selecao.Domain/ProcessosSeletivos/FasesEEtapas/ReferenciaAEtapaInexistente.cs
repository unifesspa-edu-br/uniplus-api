namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A recusa de uma configuração que cita uma etapa que o processo não tem, com o mesmo texto na gravação e na restauração.</summary>
internal static class ReferenciaAEtapaInexistente
{
    internal static DomainError PeloDesempate(int ordemDoCriterio, Guid etapaRef) => new(
        "ProcessoSeletivo.EtapaRefDesempateInexistente",
        $"O critério de desempate na ordem {ordemDoCriterio} referencia a etapa {etapaRef}, que não existe neste processo.");

    internal static DomainError PelaEliminacao(Guid etapaRef) => new(
        "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
        $"A regra de eliminação referencia a etapa {etapaRef}, que não existe neste processo.");
}
