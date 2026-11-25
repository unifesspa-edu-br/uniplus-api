namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação da instalação dos primeiros gestores de acessos.</summary>
public enum SituacaoDaInstalacao
{
    /// <summary>Iniciada; o PDF ainda não foi enviado nem confirmado.</summary>
    [CodigoDeContrato("AGUARDANDO_DOCUMENTO", "Aguardando documento")]
    AguardandoDocumento = 1,

    /// <summary>PDF confirmado; o verificador ainda não devolveu o veredicto.</summary>
    [CodigoDeContrato("DOCUMENTO_EM_VERIFICACAO", "Documento em verificação")]
    DocumentoEmVerificacao = 2,

    /// <summary>PDF limpo e promovido; o código ainda não foi pedido.</summary>
    [CodigoDeContrato("DOCUMENTO_VERIFICADO", "Documento verificado")]
    DocumentoVerificado = 3,

    /// <summary>Terminal: o verificador recusou o PDF, e a instalação recomeça.</summary>
    [CodigoDeContrato("DOCUMENTO_RECUSADO", "Documento recusado")]
    DocumentoRecusado = 4,

    /// <summary>Código enviado ao e-mail de quem executa; falta a confirmação.</summary>
    [CodigoDeContrato("AGUARDANDO_CODIGO", "Aguardando código")]
    AguardandoCodigo = 5,

    /// <summary>Terminal: confirmada; os dois gestores foram concedidos.</summary>
    [CodigoDeContrato("CONFIRMADA", "Confirmada")]
    Confirmada = 6,

    /// <summary>Terminal: substituída por outra instalação ou esgotadas as tentativas.</summary>
    [CodigoDeContrato("CANCELADA", "Cancelada")]
    Cancelada = 7,

    /// <summary>Terminal: o prazo do código esgotou.</summary>
    [CodigoDeContrato("EXPIRADA", "Expirada")]
    Expirada = 8,
}
