namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="StatusBaseLegal"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando — mesma convenção de <see cref="TipoAbrangenciaCodigo"/>.
/// </summary>
public static class StatusBaseLegalCodigo
{
    public const string Pendente = "PENDENTE";
    public const string Resolvido = "RESOLVIDO";

    public static string ToCodigo(this StatusBaseLegal status) => status switch
    {
        StatusBaseLegal.Pendente => Pendente,
        StatusBaseLegal.Resolvido => Resolvido,
        StatusBaseLegal.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(status), status, "StatusBaseLegal.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "StatusBaseLegal desconhecido."),
    };

    public static StatusBaseLegal FromCodigo(string? codigo) => codigo switch
    {
        Pendente => StatusBaseLegal.Pendente,
        Resolvido => StatusBaseLegal.Resolvido,
        _ => StatusBaseLegal.Nenhuma,
    };
}
