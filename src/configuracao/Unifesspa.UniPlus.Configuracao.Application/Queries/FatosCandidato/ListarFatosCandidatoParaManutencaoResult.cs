namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarFatosCandidatoParaManutencaoResult(
    IReadOnlyList<FatoDeManutencaoDto> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);
