namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposConceitoResult(IReadOnlyList<TipoConceitoDto> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);
