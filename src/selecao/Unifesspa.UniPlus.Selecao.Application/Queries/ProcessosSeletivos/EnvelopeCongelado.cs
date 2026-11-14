namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Lê o documento congelado de uma versão como objeto JSON, sem deixar passar exceção de análise.
/// </summary>
internal static class EnvelopeCongelado
{
    /// <remarks>
    /// Texto que não fecha como JSON faz <c>JsonNode.Parse</c> LANÇAR, e a exceção crua escaparia antes da recusa
    /// nomeada de quem chama: a pista de que a fila morta ou o log precisam é o identificador do processo.
    /// </remarks>
    internal static bool TentarLer(string congelado, [NotNullWhen(true)] out JsonObject? envelope)
    {
        try
        {
            envelope = JsonNode.Parse(congelado) as JsonObject;
        }
        catch (JsonException)
        {
            envelope = null;
        }

        return envelope is not null;
    }
}
