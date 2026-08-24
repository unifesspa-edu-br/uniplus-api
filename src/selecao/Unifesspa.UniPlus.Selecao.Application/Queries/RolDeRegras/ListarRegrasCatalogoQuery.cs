namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lista o <c>rol_de_regras</c> paginado por cursor bidirecional (ADR-0026 + ADR-0089), com
/// filtro opcional por tipo.
/// </summary>
public sealed record ListarRegrasCatalogoQuery(
    TipoRegra? Tipo,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarRegrasCatalogoResult>;
