namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Quando a data de uma <see cref="Entities.FaseCanonica"/> é fixada (UNI-REQ-0064):
/// no próprio edital (<see cref="NoEdital"/>) ou na execução do processo, pela unidade
/// responsável pela fase (<see cref="NaExecucao"/>). O código de contrato de cada membro nunca muda.
/// A ordem de declaração é a de exibição.
/// </summary>
/// <remarks>
/// Decide, sozinho, se o período (<c>Inicio</c>/<c>Fim</c>) do cronograma da fase é
/// obrigatório (<see cref="NoEdital"/>) ou opcional (<see cref="NaExecucao"/>) — ver
/// <c>FaseCronograma</c> no Módulo Seleção.
/// </remarks>
public enum OrigemDataFase
{
    /// <summary>A data é fixada no edital — período obrigatório.</summary>
    [CodigoDeContrato("NO_EDITAL", "No edital")]
    NoEdital = 1,

    /// <summary>A data não é fixada no edital: a unidade responsável pela fase a define na execução — período opcional.</summary>
    [CodigoDeContrato("NA_EXECUCAO", "Na execução do processo")]
    NaExecucao = 2,
}
