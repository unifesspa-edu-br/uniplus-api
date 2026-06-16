namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarUnidadesAtivasQuery"/>: lote de unidades já
/// projetadas + âncoras opcionais para o controller construir os cursores de
/// página anterior/próxima (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
/// <param name="Items">Unidades da página corrente, em ordem ascendente por identificador.</param>
/// <param name="AnteriorAfterId">Âncora para o cursor <c>prev</c> (primeiro item) quando há página anterior; <c>null</c> = início da coleção.</param>
/// <param name="ProximoAfterId">Âncora para o cursor <c>next</c> (último item) quando há próxima página; <c>null</c> = fim da coleção.</param>
public sealed record ListarUnidadesAtivasResult(
    IReadOnlyList<UnidadeDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
