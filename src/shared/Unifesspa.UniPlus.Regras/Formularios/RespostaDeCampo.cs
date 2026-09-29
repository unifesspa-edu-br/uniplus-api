namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

/// <summary>
/// Leitura da resposta que o candidato deu a um campo do formulário. Centraliza o que conta como
/// "sem resposta" e como extrair os códigos escolhidos, para que o avaliador e as restrições leiam a
/// resposta da mesma forma.
/// </summary>
public static class RespostaDeCampo
{
    /// <summary>
    /// Indica que o campo está sem resposta: nulo, ausente, texto em branco ou lista vazia. Uma lista
    /// vazia ou um texto em branco não são respostas — tratá-los como valor faria uma condição
    /// <c>DIFERENTE</c> ou <c>NAO_EM</c> ser satisfeita por quem não respondeu (UNI-REQ-0074).
    /// </summary>
    public static bool EstaVazia(JsonElement resposta) => resposta.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        JsonValueKind.String => string.IsNullOrWhiteSpace(resposta.GetString()),
        JsonValueKind.Array => resposta.GetArrayLength() == 0,
        _ => false,
    };

    /// <summary>
    /// Os códigos escolhidos: o texto de uma resposta escalar, ou os textos de uma lista. Devolve
    /// <see langword="null"/> quando a resposta não tem essa forma (número, booleano, objeto, ou
    /// lista com item que não é texto) — quem chama decide o que isso significa.
    /// </summary>
    public static IReadOnlyList<string>? Codigos(JsonElement resposta)
    {
        if (resposta.ValueKind == JsonValueKind.String)
        {
            return [resposta.GetString()!];
        }

        if (resposta.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<string> codigos = [];
        foreach (JsonElement item in resposta.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            codigos.Add(item.GetString()!);
        }

        return codigos;
    }
}
