namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>O código canônico do <see cref="EstadoFato"/> no wire das respostas que mostram uma avaliação.</summary>
public static class EstadoFatoCodigo
{
    public const string Indeterminado = "INDETERMINADO";
    public const string NaoAplicavel = "NAO_APLICAVEL";
    public const string Resolvido = "RESOLVIDO";
    public const string NaoInformado = "NAO_INFORMADO";

    public static string ToCodigo(this EstadoFato estado) => estado switch
    {
        EstadoFato.Indeterminado => Indeterminado,
        EstadoFato.NaoAplicavel => NaoAplicavel,
        EstadoFato.Resolvido => Resolvido,
        EstadoFato.NaoInformado => NaoInformado,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de fato fora do vocabulário."),
    };
}
