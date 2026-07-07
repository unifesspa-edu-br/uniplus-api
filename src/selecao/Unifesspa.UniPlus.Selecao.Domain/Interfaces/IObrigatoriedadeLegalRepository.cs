namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using System.Collections.Generic;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/>.
/// </summary>
/// <remarks>
/// Listagem paginada por chave (ADR-0026) com filtros admin
/// (<c>tipoEditalCodigo</c>, <c>categoria</c>, <c>vigentes</c>). Consultas
/// para o motor de conformidade (<c>ObterVigentesParaTipoEditalAsync</c>)
/// carregam regras universais (<c>"*"</c>) + específicas do tipo.
/// </remarks>
public interface IObrigatoriedadeLegalRepository : IRepository<ObrigatoriedadeLegal>
{
    /// <summary>
    /// Lista regras paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089) com filtros admin. Ordenação estável por <c>Id</c> (Guid v7
    /// cronológico); retorna até <paramref name="limit"/> itens na direção
    /// <paramref name="direction"/> a partir de <paramref name="afterId"/>,
    /// sempre em ordem ascendente, com as âncoras de <c>prev</c>/<c>next</c>.
    /// </summary>
    Task<(IReadOnlyList<ObrigatoriedadeLegal> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? tipoEditalCodigo,
        CategoriaObrigatoriedade? categoria,
        bool vigentes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o conjunto de regras vigentes aplicáveis ao tipo de edital
    /// (filtra <c>TipoEditalCodigo = '*' OR tipo</c>, vigência ativa,
    /// não soft-deleted). Caminho do motor de conformidade.
    /// </summary>
    Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterVigentesParaTipoEditalAsync(
        string tipoEditalCodigo,
        DateOnly hoje,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica colisão de <c>RegraCodigo</c> ativo (não soft-deleted).
    /// Usado para emitir <c>409 Conflict</c> com
    /// <c>uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada</c>
    /// antes de tentar a INSERT que dispararia a <c>UNIQUE</c> parcial.
    /// </summary>
    Task<bool> ExisteRegraCodigoAtivoAsync(
        string regraCodigo,
        Guid? excluirId,
        CancellationToken cancellationToken = default);
}
