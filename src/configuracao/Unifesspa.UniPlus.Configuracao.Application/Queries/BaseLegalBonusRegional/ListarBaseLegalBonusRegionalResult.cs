namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarBaseLegalBonusRegionalResult(
    IReadOnlyList<BaseLegalBonusRegionalDto> Itens,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
