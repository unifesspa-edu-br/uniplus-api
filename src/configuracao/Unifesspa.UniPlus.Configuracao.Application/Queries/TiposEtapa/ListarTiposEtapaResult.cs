namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ListarTiposEtapaResult(IReadOnlyList<TipoEtapaDto> Items, Guid? AnteriorAfterId, Guid? ProximoAfterId);
