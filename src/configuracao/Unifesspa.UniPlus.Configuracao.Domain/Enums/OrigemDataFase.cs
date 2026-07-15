namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Quem controla a data de uma <see cref="Entities.FaseCanonica"/> (UNI-REQ-0064):
/// se o próprio setor responsável pelo processo seletivo define a janela
/// (<see cref="Propria"/>) ou se ela depende de um terceiro externo ao Uni+
/// (<see cref="Delegada"/>, ex.: cronograma do MEC no SiSU). Persistido como token
/// UPPER_SNAKE (<see cref="OrigensDataFase"/>).
/// </summary>
/// <remarks>
/// Decide, sozinho, se a janela (<c>Inicio</c>/<c>Fim</c>) do cronograma da fase é
/// obrigatória (<see cref="Propria"/>) ou opcional (<see cref="Delegada"/>) — ver
/// <c>FaseCronograma</c> no Módulo Seleção. Independente de <see cref="DonoTipico"/>,
/// que é apenas rótulo orientativo e não vincula.
/// </remarks>
public enum OrigemDataFase
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>O setor responsável pelo processo seletivo controla e declara a data.</summary>
    Propria = 1,

    /// <summary>A data depende de um terceiro externo ao Uni+ (ex.: calendário do MEC).</summary>
    Delegada = 2,
}
