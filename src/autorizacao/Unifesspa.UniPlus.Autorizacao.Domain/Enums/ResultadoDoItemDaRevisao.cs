namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o revisor decidiu sobre a concessão de um item da revisão.</summary>
public enum ResultadoDoItemDaRevisao
{
    /// <summary>A concessão segue ativa.</summary>
    Confirmada = 1,

    /// <summary>A concessão foi revogada pela revisão.</summary>
    Revogada = 2,
}

/// <summary>Código textual canônico de <see cref="ResultadoDoItemDaRevisao"/>, o valor gravado e exposto.</summary>
public static class ResultadoDoItemDaRevisaoCodigo
{
    public const string Confirmada = "confirmada";
    public const string Revogada = "revogada";

    public static string ToCodigo(this ResultadoDoItemDaRevisao resultado) => resultado switch
    {
        ResultadoDoItemDaRevisao.Confirmada => Confirmada,
        ResultadoDoItemDaRevisao.Revogada => Revogada,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    public static ResultadoDoItemDaRevisao FromCodigo(string codigo) => codigo switch
    {
        Confirmada => ResultadoDoItemDaRevisao.Confirmada,
        Revogada => ResultadoDoItemDaRevisao.Revogada,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
