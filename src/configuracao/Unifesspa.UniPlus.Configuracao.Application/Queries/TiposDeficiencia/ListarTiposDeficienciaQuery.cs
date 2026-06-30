namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista tipos de deficiência vivos paginados por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
public sealed record ListarTiposDeficienciaQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarTiposDeficienciaResult>;
