namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação do documento anexado ao pedido de concessão.</summary>
public enum SituacaoDoAnexo
{
    /// <summary>A URL de envio foi emitida; o PDF ainda não foi confirmado.</summary>
    [CodigoDeContrato("AGUARDANDO_ENVIO", "Aguardando envio")]
    AguardandoEnvio = 1,

    /// <summary>PDF confirmado; o verificador ainda não devolveu o veredicto.</summary>
    [CodigoDeContrato("EM_VERIFICACAO", "Em verificação")]
    EmVerificacao = 2,

    /// <summary>PDF limpo e promovido ao armazenamento definitivo.</summary>
    [CodigoDeContrato("VERIFICADO", "Verificado")]
    Verificado = 3,

    /// <summary>Terminal: o verificador recusou o PDF, ou pediu o reenvio.</summary>
    [CodigoDeContrato("RECUSADO", "Recusado")]
    Recusado = 4,
}
