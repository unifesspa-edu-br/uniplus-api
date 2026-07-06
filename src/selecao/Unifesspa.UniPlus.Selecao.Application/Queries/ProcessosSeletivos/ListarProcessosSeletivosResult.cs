namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

/// <summary>
/// Resultado da <see cref="ListarProcessosSeletivosQuery"/>, espelhando
/// <c>ListarEditaisResult</c>.
/// </summary>
public sealed record ListarProcessosSeletivosResult(
    IReadOnlyList<ProcessoSeletivoResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
