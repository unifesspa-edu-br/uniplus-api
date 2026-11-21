namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Uma condição de predicado: fato, operador em token canônico e valor JSON.</summary>
public sealed record CondicaoDoPredicadoDto(string Fato, [property: AceitaCodigosDe<Operador>] string Operador, JsonElement Valor);
