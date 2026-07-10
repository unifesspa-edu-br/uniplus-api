namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista as versões vivas de tipos de ato, paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar (ADR-0031).
/// </summary>
/// <param name="Vigentes">
/// Quando verdadeiro (o default do endpoint), restringe às versões que valem hoje.
/// </param>
public sealed record ListarTiposAtoPublicadoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    bool Vigentes = true) : IQuery<ListarTiposAtoPublicadoResult>;
