namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposProcessoResult(IReadOnlyList<TipoProcessoDto> Items, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo);
