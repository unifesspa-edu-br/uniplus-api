namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarEditaisQuery"/>: lote de editais já projetados
/// + identificador opcional do último item para o controller construir o
/// cursor da próxima página. Não vaza entidades de domínio nem PII.
/// </summary>
/// <param name="Items">Editais da página corrente, ordenados pelo identificador.</param>
/// <param name="ProximoAfterId">Id do último item da janela quando há mais páginas; <c>null</c> sinaliza fim da coleção.</param>
public sealed record ListarEditaisResult(IReadOnlyList<EditalDto> Items, Guid? ProximoAfterId);
