namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="ResultadoPermitido"/> e o código textual
/// canônico UPPER_SNAKE do wire de comando e do envelope — mesma convenção de
/// <see cref="FundamentoIsencaoCodigo"/>, e fonte de verdade única do wire
/// format.
/// </summary>
public static class ResultadoPermitidoCodigo
{
    public const string Deferido = "DEFERIDO";
    public const string Indeferido = "INDEFERIDO";

    /// <summary>Códigos aceitos no wire, na ordem canônica — para mensagem de erro e conferência.</summary>
    public static IReadOnlyList<string> Todos { get; } = [Deferido, Indeferido];

    public static string ToCodigo(this ResultadoPermitido resultado) => resultado switch
    {
        ResultadoPermitido.Deferido => Deferido,
        ResultadoPermitido.Indeferido => Indeferido,
        ResultadoPermitido.Nenhum => throw new ArgumentOutOfRangeException(
            nameof(resultado), resultado, "ResultadoPermitido.Nenhum é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "ResultadoPermitido desconhecido."),
    };

    /// <summary>
    /// Converte o código do wire. Valor desconhecido, ausente ou fora da grafia
    /// canônica vira <see cref="ResultadoPermitido.Nenhum"/> — quem chama trata
    /// a sentinela como recusa, e não como omissão tolerável.
    /// </summary>
    public static ResultadoPermitido FromCodigo(string? codigo) => codigo switch
    {
        Deferido => ResultadoPermitido.Deferido,
        Indeferido => ResultadoPermitido.Indeferido,
        _ => ResultadoPermitido.Nenhum,
    };
}
