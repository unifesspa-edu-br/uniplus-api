namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista linhas de pesos do ENEM vivas paginadas por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
public sealed record ListarPesosAreaEnemQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarPesosAreaEnemResult>;
