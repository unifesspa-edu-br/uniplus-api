namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// O id em memória das partes que a leitura adotou pela chave que o documento declarou, e as chaves das que ela recusou. A fase e a
/// etapa montam os dois do mesmo jeito; na chave repetida vale a primeira.
/// </summary>
internal static class ChavesDasPartesLidas
{
    internal static Dictionary<string, Guid> IdPelaChave(IEnumerable<(string Chave, Guid Id)> adotadas)
    {
        Dictionary<string, Guid> idPelaChave = new(StringComparer.Ordinal);
        foreach ((string chave, Guid id) in adotadas)
        {
            idPelaChave.TryAdd(chave, id);
        }

        return idPelaChave;
    }

    internal static HashSet<string> Recusadas(IEnumerable<string> chavesDasRecusadas) => chavesDasRecusadas.ToHashSet(StringComparer.Ordinal);
}
