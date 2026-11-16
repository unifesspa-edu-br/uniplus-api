namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

/// <summary>
/// Compara a versão publicada de um esquema com a atual e devolve o que um documento válido na publicada deixaria de ser:
/// propriedade retirada, obrigatoriedade nova, tipo ou enumeração estreitados, limite apertado, padrão, formato ou
/// constante alterados e restrição nova. Aceita a propriedade opcional nova, o valor novo de enumeração e o limite relaxado.
/// </summary>
internal static class CompatibilidadeDoEsquema
{
    private static readonly string[] LimitesInferiores = ["minimum", "exclusiveMinimum", "minLength", "minItems", "minProperties", "minContains"];
    private static readonly string[] LimitesSuperiores = ["maximum", "exclusiveMaximum", "maxLength", "maxItems", "maxProperties", "maxContains"];
    private static readonly string[] IguaisPorInteiro = ["$ref", "not", "if", "oneOf"];
    private static readonly string[] RestricoesQueSoPodemSair = ["pattern", "const", "format", "multipleOf", "uniqueItems"];
    private static readonly string[] MapasDeEsquemas = ["properties", "$defs"];
    private static readonly string[] EsquemasFilhos = ["items", "then", "else", "additionalProperties", "unevaluatedProperties", "contains"];
    private static readonly string[] ListasDeEsquemas = ["allOf", "anyOf"];

    internal static IReadOnlyList<string> Violacoes(JsonNode publicado, JsonNode atual)
    {
        List<string> violacoes = [];
        Comparar(publicado, atual, "#", violacoes);
        return violacoes;
    }

    private static void Comparar(JsonNode publicado, JsonNode atual, string caminho, List<string> violacoes)
    {
        if (publicado is not JsonObject antigo || atual is not JsonObject novo)
        {
            Exigir(JsonNode.DeepEquals(publicado, atual), caminho, "esquema booleano alterado", violacoes);
            return;
        }

        CompararDeclaracoes(antigo, novo, caminho, violacoes);
        MapasDeEsquemas.ToList().ForEach(mapa => CompararMapa(antigo, novo, mapa, caminho, violacoes));
        EsquemasFilhos.ToList().ForEach(filho => CompararFilho(antigo, novo, filho, caminho, violacoes));
        ListasDeEsquemas.ToList().ForEach(lista => CompararLista(antigo, novo, lista, caminho, violacoes));
    }

    private static void CompararDeclaracoes(JsonObject antigo, JsonObject novo, string caminho, List<string> violacoes)
    {
        CompararObrigatorias(antigo, novo, caminho, violacoes);
        CompararTipo(antigo, novo, caminho, violacoes);
        CompararEnumeracao(antigo, novo, caminho, violacoes);
        LimitesInferiores.ToList().ForEach(chave => CompararLimite(antigo, novo, chave, menorEhMaisLargo: true, caminho, violacoes));
        LimitesSuperiores.ToList().ForEach(chave => CompararLimite(antigo, novo, chave, menorEhMaisLargo: false, caminho, violacoes));
        IguaisPorInteiro.ToList().ForEach(chave => Exigir(
            JsonNode.DeepEquals(antigo[chave], novo[chave]), $"{caminho}/{chave}", "mudou", violacoes));
        RestricoesQueSoPodemSair.ToList().ForEach(chave => Exigir(
            novo[chave] is null || JsonNode.DeepEquals(antigo[chave], novo[chave]), $"{caminho}/{chave}", "mudou ou passou a existir", violacoes));
    }

    private static void CompararObrigatorias(JsonObject antigo, JsonObject novo, string caminho, List<string> violacoes)
    {
        HashSet<string> antes = [.. Textos(antigo["required"])];
        foreach (string chave in Textos(novo["required"]).Where(chave => !antes.Contains(chave)))
        {
            violacoes.Add($"{caminho}/required: '{chave}' passou a ser obrigatória");
        }
    }

    private static void CompararTipo(JsonObject antigo, JsonObject novo, string caminho, List<string> violacoes)
    {
        string[] antes = Textos(antigo["type"]);
        string[] depois = Textos(novo["type"]);
        bool estreitou = depois.Length > 0 && (antes.Length == 0 || antes.Any(tipo => !depois.Contains(tipo, StringComparer.Ordinal)));
        Exigir(!estreitou, $"{caminho}/type", "estreitou", violacoes);
    }

    private static void CompararEnumeracao(JsonObject antigo, JsonObject novo, string caminho, List<string> violacoes)
    {
        if (novo["enum"] is not JsonArray depois)
        {
            return;
        }

        bool estreitou = antigo["enum"] is not JsonArray antes
            || antes.Any(valor => !depois.Any(outro => JsonNode.DeepEquals(valor, outro)));
        Exigir(!estreitou, $"{caminho}/enum", "estreitou", violacoes);
    }

    private static void CompararLimite(JsonObject antigo, JsonObject novo, string chave, bool menorEhMaisLargo, string caminho, List<string> violacoes)
    {
        if (novo[chave] is not JsonNode depois)
        {
            return;
        }

        decimal valorNovo = depois.GetValue<decimal>();
        bool apertou = antigo[chave] is not JsonNode antes
            || (menorEhMaisLargo ? valorNovo > antes.GetValue<decimal>() : valorNovo < antes.GetValue<decimal>());
        Exigir(!apertou, $"{caminho}/{chave}", "apertou", violacoes);
    }

    private static void CompararMapa(JsonObject antigo, JsonObject novo, string mapa, string caminho, List<string> violacoes)
    {
        if (antigo[mapa] is not JsonObject antes)
        {
            return;
        }

        foreach ((string nome, JsonNode? valor) in antes)
        {
            CompararEntrada(valor!, novo[mapa]?[nome], $"{caminho}/{mapa}/{nome}", violacoes);
        }

        CompararEntradasNovas(antigo, novo, mapa, caminho, violacoes);
    }

    // Uma propriedade nova restringe o que o objeto publicado já aceitava como propriedade adicional.
    private static void CompararEntradasNovas(JsonObject antigo, JsonObject novo, string mapa, string caminho, List<string> violacoes)
    {
        bool fechado = antigo["additionalProperties"]?.ToJsonString() == "false" || antigo["unevaluatedProperties"]?.ToJsonString() == "false";
        if (mapa != "properties" || fechado || novo[mapa] is not JsonObject depois)
        {
            return;
        }

        foreach (string nome in depois.Select(static par => par.Key).Where(nome => antigo[mapa]?[nome] is null))
        {
            Exigir(depois[nome]!.ToJsonString() is "true" or "{}", $"{caminho}/{mapa}/{nome}", "propriedade nova restringe o que era aceito como adicional", violacoes);
        }
    }

    private static void CompararEntrada(JsonNode antes, JsonNode? depois, string local, List<string> violacoes)
    {
        if (depois is null)
        {
            violacoes.Add($"{local}: foi retirada");
            return;
        }

        Comparar(antes, depois, local, violacoes);
    }

    private static void CompararFilho(JsonObject antigo, JsonObject novo, string chave, string caminho, List<string> violacoes)
    {
        if (novo[chave] is not JsonNode depois)
        {
            return;
        }

        if (antigo[chave] is not JsonNode antes)
        {
            Exigir(depois.ToJsonString() == "true", $"{caminho}/{chave}", "restrição nova", violacoes);
            return;
        }

        Comparar(antes, depois, $"{caminho}/{chave}", violacoes);
    }

    private static void CompararLista(JsonObject antigo, JsonObject novo, string chave, string caminho, List<string> violacoes)
    {
        JsonArray antes = antigo[chave] as JsonArray ?? [];
        JsonArray depois = novo[chave] as JsonArray ?? [];
        bool restricaoNova = chave == "allOf"
            ? depois.Count > antes.Count
            : (antigo[chave] is null ? depois.Count > 0 : novo[chave] is not null && depois.Count < antes.Count);
        Exigir(!restricaoNova, $"{caminho}/{chave}", "restrição nova", violacoes);
        foreach (int indice in Enumerable.Range(0, Math.Min(antes.Count, depois.Count)))
        {
            Comparar(antes[indice]!, depois[indice]!, $"{caminho}/{chave}/{indice}", violacoes);
        }
    }

    private static void Exigir(bool compativel, string caminho, string mudanca, List<string> violacoes)
    {
        if (!compativel)
        {
            violacoes.Add($"{caminho}: {mudanca}");
        }
    }

    private static string[] Textos(JsonNode? no) => no switch
    {
        JsonArray lista => [.. lista.Select(static item => item!.GetValue<string>())],
        JsonValue valor => [valor.GetValue<string>()],
        _ => [],
    };
}
