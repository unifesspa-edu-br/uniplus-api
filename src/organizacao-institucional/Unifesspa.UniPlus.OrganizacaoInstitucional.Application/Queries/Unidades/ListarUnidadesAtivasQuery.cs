namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista unidades ativas paginadas por cursor bidirecional (ADR-0026 +
/// ADR-0089), com filtros opcionais (issue #640). O cursor, o <c>limit</c> e a
/// <c>direction</c> já chegam decodificados — o controller decifra o cursor opaco
/// antes de despachar a query, mantendo <c>Application</c> independente de
/// <c>Infrastructure.Core</c>. A busca e o tipo chegam como o cliente os enviou:
/// quem os confere é o handler, nessa ordem.
/// </summary>
/// <param name="AfterId">Posição de continuação da página anterior; <c>null</c> retorna a primeira página.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
/// <param name="Busca">Termo de busca livre (acento/caixa-insensível) sobre sigla, nome, código, slug e alias; <c>null</c>/em branco = sem filtro textual; acima de 200 caracteres, sem contar os espaços das pontas, a consulta é recusada.</param>
/// <param name="Tipos">Valores numéricos de tipo de unidade a incluir (OR entre si), como chegaram; <c>null</c> ou vazio = sem filtro por tipo; valor que não é tipo filtrável recusa a consulta.</param>
public sealed record ListarUnidadesAtivasQuery(
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Busca,
    IReadOnlyList<int>? Tipos) : IQuery<Result<ListarUnidadesAtivasResult>>;
