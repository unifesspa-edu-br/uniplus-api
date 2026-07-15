namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="Operador"/> e o código textual canônico
/// UPPER_SNAKE que entra no envelope canônico de publicação e no wire de
/// comando (ADR-0111). Fonte de verdade única do wire format — o codec do
/// envelope e o handler de comando consomem estes métodos, nunca
/// <c>enum.ToString()</c>.
/// </summary>
public static class OperadorCodigo
{
    public const string Igual = "IGUAL";
    public const string Em = "EM";
    public const string MaiorIgual = "MAIOR_IGUAL";
    public const string MenorIgual = "MENOR_IGUAL";

    /// <summary>
    /// Converte o operador para o código canônico. O <c>switch</c> é
    /// exaustivo: um 5º operador quebra a build (CS8509 promovido a erro por
    /// <c>TreatWarningsAsErrors</c>) até este mapeamento absorvê-lo.
    /// </summary>
    public static string ToCodigo(this Operador operador) => operador switch
    {
        Operador.Igual => Igual,
        Operador.Em => Em,
        Operador.MaiorIgual => MaiorIgual,
        Operador.MenorIgual => MenorIgual,
        Operador.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(operador), operador, "Operador.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(operador), operador, "Operador desconhecido."),
    };

    /// <summary>
    /// Converte o código canônico de volta para o operador. Um código não
    /// reconhecido mapeia para o sentinela <see cref="Operador.Nenhuma"/> —
    /// que <see cref="ValueObjects.CondicaoDnf.Criar"/> já rejeita com um erro
    /// de domínio (422) claro, em vez de estourar uma exceção não tratada.
    /// </summary>
    public static Operador FromCodigo(string? codigo) => codigo switch
    {
        Igual => Operador.Igual,
        Em => Operador.Em,
        MaiorIgual => Operador.MaiorIgual,
        MenorIgual => Operador.MenorIgual,
        _ => Operador.Nenhuma,
    };
}
