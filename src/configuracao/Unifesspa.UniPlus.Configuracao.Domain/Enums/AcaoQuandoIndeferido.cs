namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ação a aplicar ao candidato de uma <see cref="Entities.Modalidade"/> de
/// concorrência quando a comprovação exigida para a reserva é indeferida
/// (UNI-REQ-0011): descreve, em domínio fechado, se o candidato é reclassificado na
/// ampla concorrência ou conforme a regra específica do edital. A ordem de declaração é a de exibição.
/// </summary>
/// <remarks>
/// <para>É opcional, e a <b>ausência é declaração, não omissão</b>: a modalidade não
/// reclassifica ninguém. Quem responde pelo destino do candidato indeferido é a
/// consequência declarada na exigência documental que o alcançou — <c>ELIMINA</c>,
/// <c>RECLASSIFICA_AC</c>, <c>REMOVE_VANTAGEM</c> ou <c>ABRE_PRAZO_PARA_REENVIO</c>, no
/// vocabulário do módulo Seleção. A conferência de coerência da publicação lê exatamente assim:
/// só confronta a consequência da exigência com esta ação <b>quando ela está declarada</b>.</para>
/// <para>Nenhuma das modalidades semeadas a preenche, inclusive as oito cotas da Lei
/// 12.711/2012 — cuja estrutura é a mais fechada do catálogo. <c>null</c> é o estado normal,
/// não pendência de decisão, e por isso não existe código para "não reclassifica": ele
/// duplicaria o mesmo estado em dois vocabulários.</para>
/// </remarks>
public enum AcaoQuandoIndeferido
{
    /// <summary>Reclassifica o candidato na ampla concorrência (AC).</summary>
    [CodigoDeContrato("RECLASSIFICAR_AC", "Reclassificar na ampla concorrência")]
    ReclassificarAc = 1,

    /// <summary>Reclassifica o candidato conforme a regra específica do edital.</summary>
    [CodigoDeContrato("RECLASSIFICAR_REGRA_EDITAL", "Reclassificar conforme a regra do edital")]
    ReclassificarRegraEdital = 2,
}
