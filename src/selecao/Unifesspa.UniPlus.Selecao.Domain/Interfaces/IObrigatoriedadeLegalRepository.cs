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
/// (<c>tipoProcessoCodigo</c>, <c>categoria</c>, dia de vigência). Consultas
/// para a conferência (<c>ObterVigentesParaTipoProcessoAsync</c>)
/// carregam regras para todos os tipos (<c>TODOS_OS_TIPOS</c>) + específicas do tipo.
/// </remarks>
public interface IObrigatoriedadeLegalRepository : IRepository<ObrigatoriedadeLegal>
{
    /// <summary>
    /// Lista regras paginadas por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089) com filtros admin. Ordenação estável por <c>Id</c> (Guid v7
    /// cronológico); retorna até <paramref name="limit"/> itens na direção
    /// <paramref name="direction"/> a partir de <paramref name="afterId"/>,
    /// sempre em ordem ascendente, com as âncoras de <c>prev</c>/<c>next</c>.
    /// Com <paramref name="vigentesEm"/>, só as regras vigentes nesse dia civil (início inclusivo, fim
    /// exclusivo); sem ele, todas.
    /// </summary>
    Task<(IReadOnlyList<ObrigatoriedadeLegal> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        DateOnly? vigentesEm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o conjunto de regras vigentes aplicáveis ao tipo de processo
    /// (filtra <c>TipoProcessoCodigo = TODOS_OS_TIPOS OR tipo</c>, vigência ativa,
    /// não soft-deleted). Caminho da conferência.
    /// </summary>
    Task<IReadOnlyList<ObrigatoriedadeLegal>> ObterVigentesParaTipoProcessoAsync(
        string tipoProcessoCodigo,
        DateOnly dataReferencia,
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
