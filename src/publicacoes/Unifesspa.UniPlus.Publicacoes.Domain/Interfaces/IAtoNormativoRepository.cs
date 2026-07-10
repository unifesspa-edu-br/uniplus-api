namespace Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Repositório da entidade <see cref="AtoNormativo"/> (schema <c>publicacoes</c>,
/// ADR-0097). O ato é append-only (ADR-0063): há inserção e leitura, nunca
/// atualização nem remoção — daí a ausência de <c>Remover</c>/<c>Atualizar</c>.
/// </summary>
public interface IAtoNormativoRepository
{
    /// <summary>Insere um novo ato. A persistência efetiva ocorre no <c>SalvarAlteracoesAsync</c> da unidade de trabalho.</summary>
    Task AdicionarAsync(AtoNormativo ato, CancellationToken cancellationToken);

    /// <summary>Carrega o ato para leitura (<c>AsNoTracking</c>) — projeção em DTO. Nulo se não existir.</summary>
    Task<AtoNormativo?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve os identificadores dos atos que compartilham a mesma numeração
    /// <c>(orgao, serie, ano, numero)</c>, excluindo <paramref name="excluirId"/>
    /// quando informado (o próprio ato, na recomputação do aviso durante a leitura).
    /// Base do aviso de número duplicado (AC4) — diagnóstico do estado atual, não
    /// prova imutável: entre esta consulta e um insert concorrente cabe uma corrida,
    /// tolerada porque o aviso não bloqueia o registro.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarIdsComMesmaNumeracaoAsync(
        string orgao,
        string serie,
        int ano,
        string numero,
        Guid? excluirId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista atos paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089):
    /// ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<AtoNormativo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);
}
