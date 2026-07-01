namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarModalidadesQuery"/>: lote de modalidades
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarModalidadesResult(
    IReadOnlyList<ModalidadeDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
