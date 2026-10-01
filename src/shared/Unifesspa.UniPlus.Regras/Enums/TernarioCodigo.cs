namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O código canônico do <see cref="Ternario"/> no wire das respostas que mostram uma avaliação, como a
/// pré-visualização do formulário: indeterminado é estado próprio, e não "falso" nem ausência.
/// </summary>
public static class TernarioCodigo
{
    public const string Verdadeiro = "VERDADEIRO";
    public const string Falso = "FALSO";
    public const string Indeterminado = "INDETERMINADO";

    public static string ToCodigo(this Ternario ternario) => ternario switch
    {
        Ternario.Verdadeiro => Verdadeiro,
        Ternario.Falso => Falso,
        Ternario.Indeterminado => Indeterminado,
        _ => throw new ArgumentOutOfRangeException(nameof(ternario), ternario, "Ternário fora do vocabulário."),
    };
}
