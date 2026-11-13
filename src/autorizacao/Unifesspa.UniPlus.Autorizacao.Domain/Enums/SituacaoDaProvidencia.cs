namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// A situação da providência como a leitura a mostra: o estado gravado e, além dele, a providência aberta cuja concessão
/// deixou de valer, por revogação feita por outro caminho ou por vencimento, que fica prejudicada sem que nada seja
/// gravado (UNI-REQ-0376).
/// </summary>
public enum SituacaoDaProvidencia
{
    Aberta = 1,
    Respondida = 2,
    Atendida = 3,

    /// <summary>Aberta, mas a concessão já não está ativa no instante da leitura.</summary>
    Prejudicada = 4,
}

/// <summary>Código textual canônico de <see cref="SituacaoDaProvidencia"/>, o valor exposto e aceito no filtro.</summary>
public static class SituacaoDaProvidenciaCodigo
{
    public const string Aberta = "aberta";
    public const string Respondida = "respondida";
    public const string Atendida = "atendida";
    public const string Prejudicada = "prejudicada";

    public static string ToCodigo(this SituacaoDaProvidencia situacao) => situacao switch
    {
        SituacaoDaProvidencia.Aberta => Aberta,
        SituacaoDaProvidencia.Respondida => Respondida,
        SituacaoDaProvidencia.Atendida => Atendida,
        SituacaoDaProvidencia.Prejudicada => Prejudicada,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    /// <summary>A situação do código, sem distinguir maiúsculas nem espaços nas pontas; nula quando o código não existe.</summary>
    public static SituacaoDaProvidencia? DoCodigo(string? codigo) => codigo?.Trim().ToLowerInvariant() switch
    {
        Aberta => SituacaoDaProvidencia.Aberta,
        Respondida => SituacaoDaProvidencia.Respondida,
        Atendida => SituacaoDaProvidencia.Atendida,
        Prejudicada => SituacaoDaProvidencia.Prejudicada,
        _ => null,
    };
}
