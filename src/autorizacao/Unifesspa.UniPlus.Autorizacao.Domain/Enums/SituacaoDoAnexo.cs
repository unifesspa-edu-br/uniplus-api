namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação do documento anexado ao pedido de concessão.</summary>
public enum SituacaoDoAnexo
{
    /// <summary>A URL de envio foi emitida; o PDF ainda não foi confirmado.</summary>
    AguardandoEnvio = 1,

    /// <summary>PDF confirmado; o verificador ainda não devolveu o veredicto.</summary>
    EmVerificacao = 2,

    /// <summary>PDF limpo e promovido ao armazenamento definitivo.</summary>
    Verificado = 3,

    /// <summary>Terminal: o verificador recusou o PDF, ou pediu o reenvio.</summary>
    Recusado = 4,
}

/// <summary>Código textual canônico de <see cref="SituacaoDoAnexo"/>, o valor gravado e exposto.</summary>
public static class SituacaoDoAnexoCodigo
{
    public const string AguardandoEnvio = "aguardando_envio";
    public const string EmVerificacao = "em_verificacao";
    public const string Verificado = "verificado";
    public const string Recusado = "recusado";

    public static string ToCodigo(this SituacaoDoAnexo situacao) => situacao switch
    {
        SituacaoDoAnexo.AguardandoEnvio => AguardandoEnvio,
        SituacaoDoAnexo.EmVerificacao => EmVerificacao,
        SituacaoDoAnexo.Verificado => Verificado,
        SituacaoDoAnexo.Recusado => Recusado,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDoAnexo FromCodigo(string codigo) => codigo switch
    {
        AguardandoEnvio => SituacaoDoAnexo.AguardandoEnvio,
        EmVerificacao => SituacaoDoAnexo.EmVerificacao,
        Verificado => SituacaoDoAnexo.Verificado,
        Recusado => SituacaoDoAnexo.Recusado,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
