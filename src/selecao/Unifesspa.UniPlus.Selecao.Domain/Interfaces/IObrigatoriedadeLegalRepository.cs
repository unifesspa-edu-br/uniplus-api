namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using System.Collections.Generic;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
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
    /// Lista regras paginadas com filtros admin. Ordenação estável por
    /// <c>Id</c> (Guid v7 cronológico). <paramref name="afterId"/> aplica
    /// keyset; <paramref name="take"/> limita a janela.
    /// </summary>
    Task<IReadOnlyList<ObrigatoriedadeLegal>> ListarPaginadoAsync(
        Guid? afterId,
        int take,
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

    /// <summary>
    /// Lê o <c>RegrasJson</c> do <c>EditalGovernanceSnapshot</c> de um edital
    /// publicado (ADR-0058 §"Snapshot-on-bind"). Retorna <see langword="null"/>
    /// quando o edital ainda não foi publicado — <c>GET /conformidade-historica</c>
    /// emite 404 com <c>uniplus.selecao.conformidade.snapshot_nao_disponivel</c>.
    /// O preenchimento da tabela é responsabilidade de #462 (US-F4-04).
    /// </summary>
    Task<string?> ObterSnapshotConformidadeJsonAsync(
        Guid editalId,
        CancellationToken cancellationToken = default);
}
