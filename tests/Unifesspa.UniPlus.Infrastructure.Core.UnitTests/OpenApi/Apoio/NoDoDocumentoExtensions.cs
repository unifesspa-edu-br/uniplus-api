namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using System.Text.Json.Nodes;

/// <summary>Leitura de um nó do documento publicado que falha dizendo o que faltou, em vez de devolver nulo.</summary>
internal static class NoDoDocumentoExtensions
{
    public static JsonNode Campo(this JsonNode no, string nome)
    {
        ArgumentNullException.ThrowIfNull(no);

        return no[nome] ?? throw new InvalidOperationException($"O nó não tem o campo {nome}.");
    }

    public static JsonNode Elemento(this JsonArray lista, int posicao)
    {
        ArgumentNullException.ThrowIfNull(lista);

        return lista[posicao] ?? throw new InvalidOperationException($"A lista não tem valor na posição {posicao}.");
    }

    public static string Texto(this JsonNode no, string nome) => no.Campo(nome).GetValue<string>();

    public static IEnumerable<string> Textos(this JsonNode no)
    {
        ArgumentNullException.ThrowIfNull(no);

        return no.AsArray().Select(static item => item?.GetValue<string>() ?? throw new InvalidOperationException("A lista tem um item nulo."));
    }
}
