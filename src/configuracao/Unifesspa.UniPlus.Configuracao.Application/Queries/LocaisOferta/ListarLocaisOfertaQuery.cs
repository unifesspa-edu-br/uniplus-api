namespace Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista locais de oferta vivos paginados por cursor bidirecional (ADR-0026 +
/// ADR-0089). O controller decifra o cursor opaco e valida limit/direction
/// antes de despachar.
/// </summary>
public sealed record ListarLocaisOfertaQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarLocaisOfertaResult>;
