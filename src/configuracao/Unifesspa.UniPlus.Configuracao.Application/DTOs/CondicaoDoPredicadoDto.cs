namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json;

/// <summary>Uma condição de predicado: fato, operador em token canônico e valor JSON.</summary>
public sealed record CondicaoDoPredicadoDto(string Fato, string Operador, JsonElement Valor);
