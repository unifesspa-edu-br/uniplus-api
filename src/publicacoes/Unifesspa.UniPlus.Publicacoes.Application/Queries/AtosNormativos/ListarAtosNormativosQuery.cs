namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista os atos publicados, paginados por cursor bidirecional (ADR-0026 +
/// ADR-0089). O controller decifra o cursor opaco e valida limit/direction antes
/// de despachar (ADR-0031). Os itens da lista não trazem avisos de numeração —
/// esses são recomputados só no detalhe, para evitar N+1.
/// </summary>
public sealed record ListarAtosNormativosQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarAtosNormativosResult>;
