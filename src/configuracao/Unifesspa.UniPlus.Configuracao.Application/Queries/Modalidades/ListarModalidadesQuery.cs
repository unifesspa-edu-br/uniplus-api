namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista modalidades de concorrência vivas paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
public sealed record ListarModalidadesQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarModalidadesResult>;
