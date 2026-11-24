namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Regra de remanejamento das vagas não preenchidas de uma
/// <see cref="Entities.Modalidade"/> de concorrência (UNI-REQ-0011): descreve, em
/// domínio fechado, para onde vagas ociosas migram (cascata legal, destino único
/// ou par cruzado). É opcional — modalidades de ampla concorrência não remanejam como cota.
/// A ordem de declaração é a de exibição.
/// </summary>
public enum RegraRemanejamento
{
    /// <summary>Segue a cascata de remanejamento legalmente estabelecida (cotas reservadas).</summary>
    [CodigoDeContrato("SEGUE_ORDEM_DE_REMANEJAMENTO", "Segue a ordem de remanejamento")]
    SegueOrdemDeRemanejamento = 1,

    /// <summary>Remaneja para um destino único informado em <c>RemanejamentoParametros.Destino</c>.</summary>
    [CodigoDeContrato("DESTINO_UNICO", "Destino único")]
    DestinoUnico = 2,

    /// <summary>Remaneja de forma cruzada entre um par (<c>Par</c>) com fallback (<c>Fallback</c>).</summary>
    [CodigoDeContrato("CRUZADO", "Cruzado")]
    Cruzado = 3,
}
