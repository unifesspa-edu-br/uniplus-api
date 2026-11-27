namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Conteúdo de <see cref="CursorPayload.SortKey"/>: o resumo da consulta, o instante congelado (vazio quando a
/// listagem não congela) e os valores das colunas de ordenação do item de fronteira. A leitura é estrita: exige o
/// número exato de partes.
/// </summary>
public static class ChaveDeContinuacao
{
    private const int PartesFixas = 2;

    /// <summary>
    /// O instante congelado que o cursor leva, para quem precisa dele antes de montar a consulta. Nulo quando a chave
    /// não tem a forma esperada ou não congela instante: a conferência do resumo recusa em seguida o que não bater.
    /// </summary>
    public static DateTimeOffset? InstanteCongelado(string? chave, int colunas) =>
        TentarLer(chave, colunas, out LeituraDaChave leitura)
        && InstanteDoCursor.TentarLer(leitura.InstanteCongelado, out DateTimeOffset instante)
            ? instante
            : null;

    internal static string Compor(string resumo, DateTimeOffset? instanteCongelado, IEnumerable<string> colunas) =>
        CompositeSortKey.Serialize(
            [resumo, instanteCongelado is { } instante ? InstanteDoCursor.Escrever(instante) : string.Empty, .. colunas]);

    internal static bool TentarLer(string? chave, int colunas, out LeituraDaChave leitura)
    {
        leitura = null!;

        if (!CompositeSortKey.TryDeserialize(chave, PartesFixas + colunas, out IReadOnlyList<string> partes))
        {
            return false;
        }

        leitura = new LeituraDaChave(partes[0], partes[1], [.. partes.Skip(PartesFixas)]);
        return true;
    }

    internal sealed record LeituraDaChave(string Resumo, string InstanteCongelado, IReadOnlyList<string> Colunas);
}
