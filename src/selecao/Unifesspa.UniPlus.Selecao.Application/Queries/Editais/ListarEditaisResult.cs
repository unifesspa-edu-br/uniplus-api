namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using DTOs;

/// <summary>
/// Resultado da <see cref="ListarEditaisQuery"/>: lote de editais já projetados
/// + identificador opcional do último item para o controller construir o
/// cursor da próxima página. Não vaza entidades de domínio nem PII.
/// </summary>
/// <param name="Items">Editais da página corrente, em ordem ascendente por identificador.</param>
/// <param name="AnteriorAfterId">Âncora para o cursor <c>prev</c> quando há página anterior; <c>null</c> = início da coleção.</param>
/// <param name="ProximoAfterId">Âncora para o cursor <c>next</c> quando há próxima página; <c>null</c> = fim da coleção.</param>
public sealed record ListarEditaisResult(
    IReadOnlyList<EditalDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
