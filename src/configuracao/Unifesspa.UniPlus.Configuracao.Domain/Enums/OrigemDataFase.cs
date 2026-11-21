namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Quando a data de uma <see cref="Entities.FaseCanonica"/> é fixada (UNI-REQ-0064):
/// no próprio edital (<see cref="NoEdital"/>) ou na execução do processo, pela unidade
/// responsável pela fase (<see cref="NaExecucao"/>). Persistido como código UPPER_SNAKE
/// (<see cref="OrigensDataFase"/>).
/// </summary>
/// <remarks>
/// Decide, sozinho, se o período (<c>Inicio</c>/<c>Fim</c>) do cronograma da fase é
/// obrigatória (<see cref="NoEdital"/>) ou opcional (<see cref="NaExecucao"/>) — ver
/// <c>FaseCronograma</c> no Módulo Seleção.
/// </remarks>
public enum OrigemDataFase
{
    /// <summary>Valor de ausência — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>A data é fixada no edital — período obrigatória.</summary>
    NoEdital = 1,

    /// <summary>A data não é fixada no edital: a unidade responsável pela fase a define na execução — período opcional.</summary>
    NaExecucao = 2,
}
