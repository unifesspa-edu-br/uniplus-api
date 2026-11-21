namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

/// <summary>
/// As bancas de uma fase ou de uma etapa lidas do envelope, na ordem do array, com o id e a posição no tipo que cada uma trazia.
/// A posição lida só vale se for a que a fase ou a etapa atribui quando as recebe.
/// </summary>
internal sealed class BancasLidas<TBanca>(string path)
    where TBanca : class
{
    private readonly List<(Guid Id, int PosicaoNoTipo)> _lidas = [];
    private readonly HashSet<Guid> _ids = [];

    internal List<TBanca> Bancas { get; } = [];

    /// <summary>Lê cada item do array com <paramref name="lerUma"/>; duas bancas do mesmo lugar com o mesmo id são recusadas.</summary>
    internal static BancasLidas<TBanca> Ler(
        LeitorEnvelope leitor, JsonArray? array, string path, Func<LeitorEnvelope, JsonObject, string, (TBanca Banca, BancaLida Lida)?> lerUma)
    {
        BancasLidas<TBanca> lidas = new(path);
        for (int j = 0; !leitor.Falhou && array is not null && j < array.Count; j++)
        {
            if (lerUma(leitor, leitor.ItemObjeto(array, j, path), $"{path}[{j}]") is { } lida)
            {
                lidas.Acrescentar(leitor, lida.Banca, lida.Lida);
            }
        }

        return leitor.Falhou ? new(path) : lidas;
    }

    /// <summary>
    /// Recusa a primeira banca cuja posição no tipo lida difere da que a fase ou a etapa lhe atribuiu, dada por id: a ordem
    /// das bancas do mesmo tipo é a posição, e uma posição trocada ou com lacuna não é a que a escrita produz.
    /// </summary>
    internal void ExigirQueSeguemARaiz(LeitorEnvelope leitor, IReadOnlyDictionary<Guid, int> posicoesDaRaiz)
    {
        for (int j = 0; !leitor.Falhou && j < _lidas.Count; j++)
        {
            if (posicoesDaRaiz[_lidas[j].Id] != _lidas[j].PosicaoNoTipo)
            {
                leitor.Malformado<int>($"{path}[{j}].posicaoNoTipo", "a posição no tipo não segue a ordem das bancas do mesmo tipo.");
            }
        }
    }

    private void Acrescentar(LeitorEnvelope leitor, TBanca banca, BancaLida lida)
    {
        if (!_ids.Add(lida.Id))
        {
            leitor.Malformado<Guid>(path, "o mesmo id aparece em mais de uma banca.");
            return;
        }

        Bancas.Add(banca);
        _lidas.Add((lida.Id, lida.PosicaoNoTipo));
    }
}
