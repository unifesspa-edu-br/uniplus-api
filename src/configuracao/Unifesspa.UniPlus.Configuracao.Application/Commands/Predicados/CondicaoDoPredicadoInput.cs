namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Uma condição de predicado na entrada: fato, operador em token canônico e valor JSON. O valor é
/// obrigatório no corpo: omitido, chegaria como elemento indefinido em vez de ser recusado.
/// </summary>
public sealed record CondicaoDoPredicadoInput(string Fato, string Operador, [property: JsonRequired] JsonElement Valor);
