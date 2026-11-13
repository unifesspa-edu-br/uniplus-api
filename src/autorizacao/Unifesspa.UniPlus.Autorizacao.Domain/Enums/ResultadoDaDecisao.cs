namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que um aprovador decidiu numa etapa do pedido.</summary>
public enum ResultadoDaDecisao
{
    Aprovado = 1,
    Recusado = 2,

    /// <summary>A etapa não decidiu: o perfil mudou depois da aprovação do gestor e o pedido voltou a aguardá-lo.</summary>
    PerfilAlterado = 3,

    /// <summary>A etapa não decidiu: o prazo que o gestor definiu venceu antes dela e o pedido voltou a aguardá-lo.</summary>
    PrazoVencido = 4,

    /// <summary>
    /// A etapa não decidiu: o perfil passou a exigir o termo de responsabilidade depois da criação, e o pedido, sem versão
    /// fixada, foi devolvido.
    /// </summary>
    TermoExigido = 5,
}

/// <summary>Código textual canônico de <see cref="ResultadoDaDecisao"/>, o valor gravado e exposto.</summary>
public static class ResultadoDaDecisaoCodigo
{
    public const string Aprovado = "aprovado";
    public const string Recusado = "recusado";
    public const string PerfilAlterado = "perfil_alterado";
    public const string PrazoVencido = "prazo_vencido";
    public const string TermoExigido = "termo_exigido";

    public static string ToCodigo(this ResultadoDaDecisao resultado) => resultado switch
    {
        ResultadoDaDecisao.Aprovado => Aprovado,
        ResultadoDaDecisao.Recusado => Recusado,
        ResultadoDaDecisao.PerfilAlterado => PerfilAlterado,
        ResultadoDaDecisao.PrazoVencido => PrazoVencido,
        ResultadoDaDecisao.TermoExigido => TermoExigido,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    public static ResultadoDaDecisao FromCodigo(string codigo) => codigo switch
    {
        Aprovado => ResultadoDaDecisao.Aprovado,
        Recusado => ResultadoDaDecisao.Recusado,
        PerfilAlterado => ResultadoDaDecisao.PerfilAlterado,
        PrazoVencido => ResultadoDaDecisao.PrazoVencido,
        TermoExigido => ResultadoDaDecisao.TermoExigido,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
