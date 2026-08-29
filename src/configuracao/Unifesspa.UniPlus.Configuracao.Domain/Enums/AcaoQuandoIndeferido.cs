namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Ação a aplicar ao candidato de uma <see cref="Entities.Modalidade"/> de
/// concorrência quando a comprovação exigida para a reserva é indeferida
/// (UNI-REQ-0011): descreve, em domínio fechado, se o candidato é reclassificado na
/// ampla concorrência ou conforme a regra específica do edital. Persistida como token
/// UPPER_SNAKE (<see cref="AcoesQuandoIndeferido"/>).
/// </summary>
/// <remarks>
/// <para>É opcional, e a <b>ausência é declaração, não omissão</b>: a modalidade não
/// reclassifica ninguém. Quem responde pelo destino do candidato indeferido é a
/// consequência declarada na exigência documental que o alcançou — <c>ELIMINA</c>,
/// <c>RECLASSIFICA_AC</c>, <c>REMOVE_VANTAGEM</c> ou <c>PENDENCIA_REENVIO</c>, no
/// vocabulário do módulo Seleção. O gate de coerência da publicação lê exatamente assim:
/// só confronta a consequência da exigência com esta ação <b>quando ela está declarada</b>.</para>
/// <para>Nenhuma das modalidades semeadas a preenche, inclusive as oito cotas da Lei
/// 12.711/2012 — cuja estrutura é a mais fechada do catálogo. <c>null</c> é o estado normal,
/// não pendência de decisão, e por isso não existe token para "não reclassifica": ele
/// duplicaria o mesmo estado em dois vocabulários.</para>
/// </remarks>
public enum AcaoQuandoIndeferido
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Reclassifica o candidato na ampla concorrência (AC).</summary>
    ReclassificarAc = 1,

    /// <summary>Reclassifica o candidato conforme a regra específica do edital.</summary>
    ReclassificarRegraEdital = 2,
}
