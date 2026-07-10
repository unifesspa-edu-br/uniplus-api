namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista as versões vivas de tipos de ato, paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar (ADR-0031).
/// </summary>
public sealed record ListarTiposAtoPublicadoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarTiposAtoPublicadoResult>;
