namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

/// <summary>Lê uma coleção ordenada de objetos, interrompendo no primeiro item recusado.</summary>
internal static class LeituraDeItens
{
    internal static List<T>? Ler<T>(LeitorEnvelope leitor, JsonArray array, string path, Func<LeitorEnvelope, JsonObject, string, T?> ler)
        where T : class
    {
        List<T> itens = [];
        for (int i = 0; i < array.Count; i++)
        {
            T? item = ler(leitor, leitor.ItemObjeto(array, i, path), $"{path}[{i}]");
            if (item is null)
            {
                return null;
            }

            itens.Add(item);
        }

        return itens;
    }
}
