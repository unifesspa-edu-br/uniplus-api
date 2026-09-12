namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

/// <summary>
/// Projeção de leitura de <c>EtapaProcesso</c> (Story #758).
/// </summary>
/// <param name="TipoEtapa">
/// Snapshot congelado do tipo (issue #1071) — fecha o round-trip GET→PUT: um cliente que
/// reler a etapa precisa de <c>OrigemId</c> para reenviar o mesmo vínculo em
/// <c>EtapaProcessoInput.TipoEtapaOrigemId</c>.
/// </param>
public sealed record EtapaProcessoDto(
    Guid Id,
    string Nome,
    CaraterEtapa Carater,
    TipoEtapaSnapshotDto TipoEtapa,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem,
    string? FaseCodigo,
    IReadOnlyList<ProdutoDaEtapaDto> Produtos,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    bool EmiteParecerIndividual,
    IReadOnlyList<BancaDaEtapaDto> Bancas,
    IReadOnlyList<RecursoDaEtapaDto> Recursos);

/// <summary>Projeção de leitura de <c>BancaDaEtapa</c>.</summary>
public sealed record BancaDaEtapaDto(Guid Id, Guid TipoBancaOrigemId, string Codigo);

/// <summary>Projeção de leitura de <c>RecursoDaEtapa</c>.</summary>
public sealed record RecursoDaEtapaDto(
    Guid Id,
    AncoraDoRecurso Ancora,
    ReferenciaRegraDto Regra,
    ArgsRegraPrazoRecursoDto Args,
    Guid ProdutoAncoraId,
    string? AtoAncoraCodigo);

/// <summary>Projeção de leitura de <c>ProdutoDaEtapa</c>.</summary>
/// <param name="Papel">
/// Token canônico UPPER_SNAKE (<c>PRELIMINAR</c>/<c>DEFINITIVO</c>), o mesmo vocabulário
/// que o produto da fase publica — o enum cru vazaria um segundo vocabulário no wire para
/// o mesmo conceito.
/// </param>
public sealed record ProdutoDaEtapaDto(Guid Id, string AtoCodigo, string? Papel);

/// <summary>Cópia por valor do tipo de etapa, projetada para leitura (ADR-0061).</summary>
public sealed record TipoEtapaSnapshotDto(Guid OrigemId, string Codigo, string Nome);
