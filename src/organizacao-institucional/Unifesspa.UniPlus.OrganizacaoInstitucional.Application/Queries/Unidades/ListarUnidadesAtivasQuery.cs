namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Lista unidades ativas paginadas por cursor bidirecional (ADR-0026 +
/// ADR-0089), com filtros opcionais (issue #640). Os parâmetros já chegam
/// decodificados — o controller decifra o cursor opaco, valida o <c>limit</c> e
/// a <c>direction</c> antes de despachar a query, mantendo <c>Application</c>
/// independente de <c>Infrastructure.Core</c>.
/// </summary>
/// <param name="AfterId">Âncora da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
/// <param name="Busca">Termo de busca livre (acento/caixa-insensível) sobre sigla, nome, código, slug e alias; <c>null</c>/vazio = sem filtro textual. O handler normaliza.</param>
/// <param name="Tipos">Tipos de unidade a incluir (OR entre si); lista vazia = sem filtro por tipo.</param>
public sealed record ListarUnidadesAtivasQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Busca,
    IReadOnlyList<TipoUnidade> Tipos) : IQuery<ListarUnidadesAtivasResult>;
