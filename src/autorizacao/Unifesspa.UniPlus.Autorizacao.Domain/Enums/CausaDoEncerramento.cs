namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que encerrou uma concessão, derivado na leitura.</summary>
public enum CausaDoEncerramento
{
    /// <summary>A concessão foi revogada, por uma pessoa ou pela janela de migração.</summary>
    Revogada = 1,

    /// <summary>A vigência da concessão chegou ao fim, sem ato gravado.</summary>
    Vencida = 2,
}

/// <summary>Código textual canônico de <see cref="CausaDoEncerramento"/>, o valor exposto.</summary>
public static class CausaDoEncerramentoCodigo
{
    public const string Revogada = "revogada";
    public const string Vencida = "vencida";

    public static string ToCodigo(this CausaDoEncerramento causa) => causa switch
    {
        CausaDoEncerramento.Revogada => Revogada,
        CausaDoEncerramento.Vencida => Vencida,
        _ => throw new ArgumentOutOfRangeException(nameof(causa), causa, null),
    };
}
