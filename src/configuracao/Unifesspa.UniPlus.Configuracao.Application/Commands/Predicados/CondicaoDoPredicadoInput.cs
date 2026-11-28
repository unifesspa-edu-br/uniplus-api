namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;

using System.ComponentModel;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Uma condição de predicado na entrada: fato, operador em token canônico e valor JSON. O valor é
/// obrigatório no contrato: omitido, chega como elemento indefinido e o domínio o recusa (422), junto das
/// demais recusas da condição.
/// </summary>
public sealed record CondicaoDoPredicadoInput(
    string Fato,
    [AceitaCodigosDe<Operador>] string? Operador,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    JsonElement Valor);
