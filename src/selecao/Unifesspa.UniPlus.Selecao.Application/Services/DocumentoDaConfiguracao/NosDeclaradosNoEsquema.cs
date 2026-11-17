namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

/// <summary>
/// Lista os pontos do documento na ordem em que ele está escrito, só os que o esquema declara; o que o esquema não conhece
/// fica de fora e é recusado depois, pela avaliação. Devolve um instantâneo, de modo que quem o consome pode alterar o
/// documento sem invalidar a enumeração.
/// </summary>
internal static class NosDeclaradosNoEsquema
{
    public static IReadOnlyList<NoDoDocumento> Listar(JsonObject documento, FormaDoNo raiz)
    {
        List<NoDoDocumento> nos = [];
        Descer(new NoDoDocumento(documento, raiz, string.Empty, string.Empty), nos);
        return nos;
    }

    private static void Descer(NoDoDocumento no, List<NoDoDocumento> nos)
    {
        nos.Add(no);
        foreach (NoDoDocumento filho in Filhos(no))
        {
            Descer(filho, nos);
        }
    }

    private static IEnumerable<NoDoDocumento> Filhos(NoDoDocumento no) => no.Valor switch
    {
        JsonObject objeto => FilhosDoObjeto(no, objeto),
        JsonArray lista when no.Forma.Itens is not null => FilhosDaLista(no, lista, no.Forma.Itens),
        _ => [],
    };

    private static IEnumerable<NoDoDocumento> FilhosDoObjeto(NoDoDocumento pai, JsonObject objeto)
    {
        string ancestral = ParteAncestralDosFilhos(pai);
        foreach ((string nome, JsonNode? valor) in objeto)
        {
            if (valor is not null && pai.Forma.Propriedades.TryGetValue(nome, out FormaDoNo? forma))
            {
                yield return new NoDoDocumento(valor, forma, PonteiroJson.Acrescentar(pai.Ponteiro, nome), ancestral);
            }
        }
    }

    private static IEnumerable<NoDoDocumento> FilhosDaLista(NoDoDocumento pai, JsonArray lista, FormaDoNo itens)
    {
        string ancestral = ParteAncestralDosFilhos(pai);
        for (int indice = 0; indice < lista.Count; indice++)
        {
            if (lista[indice] is { } elemento)
            {
                string ponteiro = PonteiroJson.Acrescentar(pai.Ponteiro, indice.ToString(CultureInfo.InvariantCulture));
                yield return new NoDoDocumento(elemento, itens, ponteiro, ancestral);
            }
        }
    }

    private static string ParteAncestralDosFilhos(NoDoDocumento pai) => pai.Forma.Parte is null ? pai.ParteAncestral : pai.Ponteiro;
}
