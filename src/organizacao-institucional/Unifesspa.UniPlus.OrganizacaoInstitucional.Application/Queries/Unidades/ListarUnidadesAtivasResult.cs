namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarUnidadesAtivasQuery"/>: lote de unidades já
/// projetadas + identificador opcional do último item para o controller
/// construir o cursor da próxima página (ADR-0026). Não vaza entidades de
/// domínio.
/// </summary>
/// <param name="Items">Unidades da página corrente, ordenadas pelo identificador.</param>
/// <param name="ProximoAfterId">Id do último item da janela quando há mais páginas; <c>null</c> sinaliza fim da coleção.</param>
public sealed record ListarUnidadesAtivasResult(IReadOnlyList<UnidadeDto> Items, Guid? ProximoAfterId);
