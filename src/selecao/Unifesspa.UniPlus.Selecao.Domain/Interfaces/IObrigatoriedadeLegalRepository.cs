namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using System.Collections.Generic;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Repositório de <see cref="ObrigatoriedadeLegal"/> + reconciliação atômica
/// da junction temporal <c>obrigatoriedade_legal_areas_de_interesse</c>
/// (ADR-0060). O repositório é o único ponto que escreve nessa junction —
/// o template <c>AreaVisibilityConfiguration</c> não tem nav property no
/// modelo EF.
/// </summary>
/// <remarks>
/// Listagem paginada por chave (ADR-0026) com filtros admin
/// (<c>tipoEditalCodigo</c>, <c>categoria</c>, <c>proprietario</c>,
/// <c>vigentes</c>). Consultas para o motor de conformidade
/// (<c>ObterVigentesParaTipoEditalAsync</c>) carregam regras universais
/// (<c>"*"</c>) + específicas do tipo.
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
        AreaCodigo? proprietario,
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
    /// Persiste uma nova regra junto com seus bindings temporais. POST do
    /// admin CRUD — bindings recebem <c>ValidoDe = now</c>, <c>ValidoAte = null</c>.
    /// </summary>
    Task AdicionarComBindingsAsync(
        ObrigatoriedadeLegal regra,
        IReadOnlySet<AreaCodigo> areasDeInteresse,
        DateTimeOffset agora,
        string adicionadoPor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Diff-aplica novo conjunto de bindings: insere novos
    /// (<c>ValidoDe = now</c>); fecha removidos (<c>ValidoAte = now</c>);
    /// preserva intactos. PUT do admin CRUD respeitando histórico temporal
    /// da junction (ADR-0060).
    /// </summary>
    Task ReconciliarBindingsAsync(
        ObrigatoriedadeLegal regra,
        IReadOnlySet<AreaCodigo> novasAreasDeInteresse,
        DateTimeOffset agora,
        string adicionadoPor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê o set vigente de áreas para a regra (bindings com
    /// <c>ValidoAte IS NULL</c>). Usado pelo handler do PUT para reidratar
    /// o set in-memory antes do <c>Atualizar()</c> da entity (semântica
    /// full-replace exige caller passar valores correntes).
    /// </summary>
    Task<IReadOnlySet<AreaCodigo>> ObterAreasVigentesAsync(
        Guid regraId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o map id → áreas vigentes em batch para um conjunto de regras —
    /// evita N+1 quando o handler do <c>GET</c> paginado precisa hidratar
    /// <c>AreasDeInteresse</c> do DTO de cada item.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<AreaCodigo>>> ObterAreasVigentesPorIdsAsync(
        IReadOnlyCollection<Guid> regraIds,
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
