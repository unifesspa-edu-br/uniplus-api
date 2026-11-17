namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// Põe as causas na ordem única do diagnóstico: a posição do ponto no documento, o ponto que contém antes dos que ele contém,
/// e uma causa só por ponteiro (a primeira recebida, então quem chama passa antes a de maior precedência). O que o documento
/// não contém, como o campo obrigatório ausente, vem depois do que o objeto contém e segue a ordem em que foi recebido.
/// </summary>
public static class OrdemDasCausas
{
    public static IReadOnlyList<FieldError> Ordenar(IEnumerable<FieldError> causas, JsonNode documento)
    {
        ArgumentNullException.ThrowIfNull(causas);
        ArgumentNullException.ThrowIfNull(documento);

        return
        [
            .. causas.GroupBy(static causa => causa.Field ?? string.Empty, StringComparer.Ordinal)
                .Select(static grupo => grupo.First())
                .OrderBy(causa => PosicaoDe(documento, causa.Field ?? string.Empty), ComparadorDePosicao.Instancia),
        ];
    }

    private static int[] PosicaoDe(JsonNode documento, string ponteiro)
    {
        string prefixo = string.Empty;
        List<int> posicao = [];
        foreach (string segmento in PonteiroJson.Segmentos(ponteiro))
        {
            posicao.Add(PosicaoDoSegmento(PonteiroJson.Resolver(documento, prefixo), segmento));
            prefixo = PonteiroJson.Acrescentar(prefixo, segmento);
        }

        return [.. posicao];
    }

    private static int PosicaoDoSegmento(JsonNode? pai, string segmento) => pai switch
    {
        JsonObject objeto => IndiceDaPropriedade(objeto, segmento),
        JsonArray => int.TryParse(segmento, NumberStyles.None, CultureInfo.InvariantCulture, out int indice) ? indice : int.MaxValue,
        _ => int.MaxValue,
    };

    private static int IndiceDaPropriedade(JsonObject objeto, string nome)
    {
        int indice = objeto.Select(static propriedade => propriedade.Key).ToList().IndexOf(nome);
        return indice < 0 ? int.MaxValue : indice;
    }

    private sealed class ComparadorDePosicao : IComparer<int[]>
    {
        public static ComparadorDePosicao Instancia { get; } = new();

        public int Compare(int[]? x, int[]? y)
        {
            int comum = Math.Min(x!.Length, y!.Length);
            for (int indice = 0; indice < comum; indice++)
            {
                int diferenca = x[indice].CompareTo(y[indice]);
                if (diferenca != 0)
                {
                    return diferenca;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
