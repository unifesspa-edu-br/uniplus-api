namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed record ListarBasesLegaisDoFadaResult(
    IReadOnlyList<BaseLegalDoFadaDto> Itens,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
