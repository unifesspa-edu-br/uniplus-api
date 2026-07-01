namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Ação a aplicar ao candidato de uma <see cref="Entities.Modalidade"/> de
/// concorrência quando o pedido de reserva é indeferido (UNI-REQ-0011): descreve,
/// em domínio fechado, se o candidato é reclassificado na ampla concorrência ou
/// conforme a regra específica do edital. Persistida como token UPPER_SNAKE
/// (<see cref="AcoesQuandoIndeferido"/>). É opcional.
/// </summary>
public enum AcaoQuandoIndeferido
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Reclassifica o candidato na ampla concorrência (AC).</summary>
    ReclassificarAc = 1,

    /// <summary>Reclassifica o candidato conforme a regra específica do edital.</summary>
    ReclassificarRegraEdital = 2,
}
