namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Como a janela de migração terminou.</summary>
public enum FormaDoEncerramento
{
    /// <summary>Não restou concessão pendente.</summary>
    Concluida = 1,

    /// <summary>As 72 horas acabaram.</summary>
    Prazo = 2,

    /// <summary>A gestão de acessos encerrou a versão anterior, com motivo.</summary>
    Manual = 3,
}

/// <summary>Código textual canônico de <see cref="FormaDoEncerramento"/>, o valor gravado e exposto.</summary>
public static class FormaDoEncerramentoCodigo
{
    public const string Concluida = "concluida";
    public const string Prazo = "prazo";
    public const string Manual = "manual";

    public static string ToCodigo(this FormaDoEncerramento forma) => forma switch
    {
        FormaDoEncerramento.Concluida => Concluida,
        FormaDoEncerramento.Prazo => Prazo,
        FormaDoEncerramento.Manual => Manual,
        _ => throw new ArgumentOutOfRangeException(nameof(forma), forma, null),
    };

    public static FormaDoEncerramento FromCodigo(string codigo) => codigo switch
    {
        Concluida => FormaDoEncerramento.Concluida,
        Prazo => FormaDoEncerramento.Prazo,
        Manual => FormaDoEncerramento.Manual,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
