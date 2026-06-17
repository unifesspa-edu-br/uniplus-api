namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista campi vivos paginados por cursor bidirecional (ADR-0026 + ADR-0089).
/// O controller decifra o cursor opaco e valida limit/direction antes de
/// despachar, mantendo a Application independente de Infrastructure.Core.
/// </summary>
public sealed record ListarCampiQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarCampiResult>;
