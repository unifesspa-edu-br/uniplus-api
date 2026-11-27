namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// O id em memória das partes que a leitura adotou pela chave que o documento declarou, e as chaves das que ela recusou. A fase e a
/// etapa montam os dois do mesmo jeito; na chave repetida vale a primeira.
/// </summary>
internal static class ChavesDasPartesLidas
{
    internal static Dictionary<string, Guid> IdPelaChave(IEnumerable<(string Chave, Guid Id)> adotadas) => PelaChave(adotadas);

    internal static Dictionary<string, T> PelaChave<T>(IEnumerable<(string Chave, T Valor)> adotadas)
    {
        Dictionary<string, T> pelaChave = new(StringComparer.Ordinal);
        foreach ((string chave, T valor) in adotadas)
        {
            pelaChave.TryAdd(chave, valor);
        }

        return pelaChave;
    }

    internal static HashSet<string> Recusadas(IEnumerable<string> chavesDasRecusadas) => chavesDasRecusadas.ToHashSet(StringComparer.Ordinal);
}
