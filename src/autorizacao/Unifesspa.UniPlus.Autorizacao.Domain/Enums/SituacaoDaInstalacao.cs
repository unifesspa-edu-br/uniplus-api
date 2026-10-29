namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação da instalação dos primeiros gestores de acessos.</summary>
public enum SituacaoDaInstalacao
{
    /// <summary>Iniciada; o PDF ainda não foi enviado nem confirmado.</summary>
    AguardandoDocumento = 1,

    /// <summary>PDF confirmado; o verificador ainda não devolveu o veredicto.</summary>
    DocumentoEmVerificacao = 2,

    /// <summary>PDF limpo e promovido; o código ainda não foi pedido.</summary>
    DocumentoVerificado = 3,

    /// <summary>Terminal: o verificador recusou o PDF, e a instalação recomeça.</summary>
    DocumentoRecusado = 4,

    /// <summary>Código enviado ao e-mail de quem executa; falta a confirmação.</summary>
    AguardandoCodigo = 5,

    /// <summary>Terminal: confirmada; os dois gestores foram concedidos.</summary>
    Confirmada = 6,

    /// <summary>Terminal: substituída por outra instalação ou esgotadas as tentativas.</summary>
    Cancelada = 7,

    /// <summary>Terminal: o prazo do código esgotou.</summary>
    Expirada = 8,
}

/// <summary>Código textual canônico de <see cref="SituacaoDaInstalacao"/>, o valor gravado e exposto.</summary>
public static class SituacaoDaInstalacaoCodigo
{
    public const string AguardandoDocumento = "aguardando_documento";
    public const string DocumentoEmVerificacao = "documento_em_verificacao";
    public const string DocumentoVerificado = "documento_verificado";
    public const string DocumentoRecusado = "documento_recusado";
    public const string AguardandoCodigo = "aguardando_codigo";
    public const string Confirmada = "confirmada";
    public const string Cancelada = "cancelada";
    public const string Expirada = "expirada";

    public static string ToCodigo(this SituacaoDaInstalacao situacao) => situacao switch
    {
        SituacaoDaInstalacao.AguardandoDocumento => AguardandoDocumento,
        SituacaoDaInstalacao.DocumentoEmVerificacao => DocumentoEmVerificacao,
        SituacaoDaInstalacao.DocumentoVerificado => DocumentoVerificado,
        SituacaoDaInstalacao.DocumentoRecusado => DocumentoRecusado,
        SituacaoDaInstalacao.AguardandoCodigo => AguardandoCodigo,
        SituacaoDaInstalacao.Confirmada => Confirmada,
        SituacaoDaInstalacao.Cancelada => Cancelada,
        SituacaoDaInstalacao.Expirada => Expirada,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDaInstalacao FromCodigo(string codigo) => codigo switch
    {
        AguardandoDocumento => SituacaoDaInstalacao.AguardandoDocumento,
        DocumentoEmVerificacao => SituacaoDaInstalacao.DocumentoEmVerificacao,
        DocumentoVerificado => SituacaoDaInstalacao.DocumentoVerificado,
        DocumentoRecusado => SituacaoDaInstalacao.DocumentoRecusado,
        AguardandoCodigo => SituacaoDaInstalacao.AguardandoCodigo,
        Confirmada => SituacaoDaInstalacao.Confirmada,
        Cancelada => SituacaoDaInstalacao.Cancelada,
        Expirada => SituacaoDaInstalacao.Expirada,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
