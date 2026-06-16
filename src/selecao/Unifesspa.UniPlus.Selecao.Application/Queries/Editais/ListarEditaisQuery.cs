namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista editais paginados por cursor bidirecional (ADR-0026 + ADR-0089). Os
/// parâmetros já chegam decodificados — o controller decifra o cursor opaco e
/// valida <c>limit</c>/<c>direction</c> antes de despachar a query, mantendo
/// <c>Application</c> independente de <c>Infrastructure.Core</c>.
/// </summary>
/// <param name="AfterId">Âncora da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
public sealed record ListarEditaisQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarEditaisResult>;
