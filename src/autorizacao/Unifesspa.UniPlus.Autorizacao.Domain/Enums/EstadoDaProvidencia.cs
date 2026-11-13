namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O estado gravado da providência, que só os atos dela mudam (UNI-REQ-0376). Não existe estado "prejudicada": ela é
/// derivada na leitura (<see cref="SituacaoDaProvidencia"/>), e o tipo gravado não tem como representá-la.
/// </summary>
public enum EstadoDaProvidencia
{
    /// <summary>Aguarda resposta ou atendimento.</summary>
    Aberta = 1,

    /// <summary>Respondida com motivo, sem revogar a concessão.</summary>
    Respondida = 2,

    /// <summary>Atendida pela revogação vinculada da concessão.</summary>
    Atendida = 3,
}

/// <summary>Código textual canônico de <see cref="EstadoDaProvidencia"/>, o valor gravado.</summary>
public static class EstadoDaProvidenciaCodigo
{
    public const string Aberta = "aberta";
    public const string Respondida = "respondida";
    public const string Atendida = "atendida";

    public static string ToCodigo(this EstadoDaProvidencia estado) => estado switch
    {
        EstadoDaProvidencia.Aberta => Aberta,
        EstadoDaProvidencia.Respondida => Respondida,
        EstadoDaProvidencia.Atendida => Atendida,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    public static EstadoDaProvidencia FromCodigo(string codigo) => codigo switch
    {
        Aberta => EstadoDaProvidencia.Aberta,
        Respondida => EstadoDaProvidencia.Respondida,
        Atendida => EstadoDaProvidencia.Atendida,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
