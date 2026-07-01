namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista tipos de banca vivos paginados por cursor bidirecional (ADR-0026 +
/// ADR-0089). O controller decifra o cursor opaco e valida limit/direction antes de
/// despachar.
/// </summary>
public sealed record ListarTiposBancaQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarTiposBancaResult>;
