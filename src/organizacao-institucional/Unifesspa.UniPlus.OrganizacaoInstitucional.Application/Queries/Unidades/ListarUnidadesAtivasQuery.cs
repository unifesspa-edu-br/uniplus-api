namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Lista unidades ativas paginadas por cursor (ADR-0026). Os parâmetros já
/// chegam decodificados — o controller decifra o cursor opaco e valida o
/// <c>limit</c> antes de despachar a query, mantendo <c>Application</c>
/// independente de <c>Infrastructure.Core</c>.
/// </summary>
/// <param name="AfterId">Identificador do último item da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
public sealed record ListarUnidadesAtivasQuery(Guid? AfterId, int Limit) : IQuery<ListarUnidadesAtivasResult>;
