namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Lista unidades ativas paginadas por cursor (ADR-0026), com filtros
/// opcionais (issue #640). Os parâmetros já chegam decodificados — o controller
/// decifra o cursor opaco e valida o <c>limit</c> antes de despachar a query,
/// mantendo <c>Application</c> independente de <c>Infrastructure.Core</c>.
/// </summary>
/// <param name="AfterId">Identificador do último item da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Busca">Termo de busca livre (acento/caixa-insensível) sobre sigla, nome, código, slug e alias; <c>null</c>/vazio = sem filtro textual. O handler normaliza.</param>
/// <param name="Tipos">Tipos de unidade a incluir (OR entre si); lista vazia = sem filtro por tipo.</param>
public sealed record ListarUnidadesAtivasQuery(
    Guid? AfterId,
    int Limit,
    string? Busca,
    IReadOnlyList<TipoUnidade> Tipos) : IQuery<ListarUnidadesAtivasResult>;
