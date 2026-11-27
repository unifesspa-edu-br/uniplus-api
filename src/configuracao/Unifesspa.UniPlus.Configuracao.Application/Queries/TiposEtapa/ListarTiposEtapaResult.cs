namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposEtapaResult(IReadOnlyList<TipoEtapaDto> Items, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo);
