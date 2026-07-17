namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="UnidadeIdade"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando — mesma convenção de <see cref="OperadorCodigo"/>.
/// </summary>
public static class UnidadeIdadeCodigo
{
    public const string Dias = "DIAS";
    public const string Meses = "MESES";
    public const string Anos = "ANOS";

    public static string ToCodigo(this UnidadeIdade unidade) => unidade switch
    {
        UnidadeIdade.Dias => Dias,
        UnidadeIdade.Meses => Meses,
        UnidadeIdade.Anos => Anos,
        UnidadeIdade.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(unidade), unidade, "UnidadeIdade.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(unidade), unidade, "UnidadeIdade desconhecida."),
    };

    /// <summary>
    /// <see langword="null"/> tanto para código ausente quanto para código fora do
    /// domínio — a coerência tudo-nulo OU completo de <see cref="ValueObjects.IdadeMaximaEmissao"/>
    /// depende de distinguir "ausente" de "presente com valor concreto", não de um
    /// terceiro estado sentinela; a checagem de que um código NÃO NULO pertence ao
    /// domínio é do <c>FluentValidation</c> (Application), antes deste método ser chamado.
    /// </summary>
    public static UnidadeIdade? FromCodigo(string? codigo) => codigo switch
    {
        Dias => UnidadeIdade.Dias,
        Meses => UnidadeIdade.Meses,
        Anos => UnidadeIdade.Anos,
        _ => null,
    };
}
