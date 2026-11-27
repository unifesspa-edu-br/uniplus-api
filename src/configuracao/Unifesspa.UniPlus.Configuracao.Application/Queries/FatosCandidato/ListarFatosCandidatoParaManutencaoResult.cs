namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarFatosCandidatoParaManutencaoResult(
    IReadOnlyList<FatoDeManutencaoDto> Items, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo);
