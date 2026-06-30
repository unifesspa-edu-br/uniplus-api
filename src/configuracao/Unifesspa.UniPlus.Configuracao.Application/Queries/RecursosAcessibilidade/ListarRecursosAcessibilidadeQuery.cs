namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista recursos de acessibilidade vivos paginados por cursor bidirecional
/// (ADR-0026 + ADR-0089). O controller decifra o cursor opaco e valida
/// limit/direction antes de despachar.
/// </summary>
public sealed record ListarRecursosAcessibilidadeQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarRecursosAcessibilidadeResult>;
