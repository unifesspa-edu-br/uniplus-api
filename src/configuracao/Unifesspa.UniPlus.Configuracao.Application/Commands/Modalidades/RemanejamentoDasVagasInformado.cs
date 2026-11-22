namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Para onde vão as vagas não preenchidas da modalidade: a regra, como código de contrato, e os códigos de modalidade que a
/// regra pede (<c>Destino</c> para destino único; <c>Par</c> e, opcionalmente, <c>Fallback</c> para cruzado). A análise e
/// a coerência com a natureza ocorrem no agregado.
/// </summary>
public sealed record RemanejamentoDasVagasInformado(
    [property: AceitaCodigosDe<RegraRemanejamento>] string? Regra,
    string? Destino = null,
    string? Par = null,
    string? Fallback = null);
