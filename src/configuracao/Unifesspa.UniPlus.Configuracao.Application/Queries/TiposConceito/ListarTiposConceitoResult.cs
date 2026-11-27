namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposConceitoResult(IReadOnlyList<TipoConceitoDto> Items, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo);
