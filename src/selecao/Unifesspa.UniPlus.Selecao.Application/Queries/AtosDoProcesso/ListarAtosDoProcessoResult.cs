namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Atos do processo, do mais recente para o mais antigo. Sem paginação: é o histórico de publicações
/// de um único processo, e cortá-lo esconderia justamente a pendência antiga que a consulta destaca.
/// </summary>
public sealed record ListarAtosDoProcessoResult(IReadOnlyList<AtoDoProcessoDto> Items);
