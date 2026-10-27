namespace Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;

using DTOs;

public sealed record ListarProcessosAcompanhamentoResult(
    IReadOnlyList<ProcessoAcompanhamentoResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
