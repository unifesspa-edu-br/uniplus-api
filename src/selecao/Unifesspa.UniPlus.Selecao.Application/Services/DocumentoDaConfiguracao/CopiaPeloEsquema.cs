namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class CopiaPeloEsquema
{
    public static JsonObject Copiar(JsonObject origem, FormaDoNo forma)
    {
        JsonObject copia = [];
        foreach ((string nome, FormaDoNo filho) in forma.Propriedades)
        {
            if (TentarObter(origem, nome, filho, out JsonNode? valor) && (valor is not null || filho.AceitaNulo))
            {
                copia[nome] = CopiarValor(valor, filho);
            }
        }

        return copia;
    }

    private static bool TentarObter(JsonObject origem, string nome, FormaDoNo forma, out JsonNode? valor) =>
        origem.TryGetPropertyValue(nome, out valor)
        || (forma.SomenteLeitura
            && origem["contexto"] is JsonObject contexto
            && contexto.TryGetPropertyValue(nome, out valor));

    private static JsonNode? CopiarValor(JsonNode? valor, FormaDoNo forma) => valor switch
    {
        null => null,
        JsonObject objeto => Copiar(objeto, forma),
        JsonArray lista when forma.Itens is { } itens => CopiarLista(lista, itens),
        JsonArray => new JsonArray(),
        JsonValue escalar when forma.AceitaNumeroMasNaoTexto && TentarDecimal(escalar, out decimal numero) => JsonValue.Create(numero),
        _ => valor.DeepClone(),
    };

    private static JsonArray CopiarLista(JsonArray origem, FormaDoNo forma)
    {
        JsonArray copia = [];
        foreach (JsonNode? valor in origem)
        {
            if (valor is not null || forma.AceitaNulo)
            {
                copia.Add(CopiarValor(valor, forma));
            }
        }

        return copia;
    }

    private static bool TentarDecimal(JsonValue valor, out decimal numero)
    {
        numero = default;
        if (!valor.TryGetValue(out string? texto) || !decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numero))
        {
            return false;
        }

        int ponto = texto.IndexOf('.', StringComparison.Ordinal);
        int escala = ponto < 0 ? 0 : texto.Length - ponto - 1;
        return escala <= 28 && string.Equals(texto, HashCanonicalComputer.SerializeDecimalCanonical(numero, escala), StringComparison.Ordinal);
    }
}
