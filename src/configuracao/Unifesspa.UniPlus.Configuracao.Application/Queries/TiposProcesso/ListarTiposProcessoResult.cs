namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposProcessoResult(IReadOnlyList<TipoProcessoDto> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);
