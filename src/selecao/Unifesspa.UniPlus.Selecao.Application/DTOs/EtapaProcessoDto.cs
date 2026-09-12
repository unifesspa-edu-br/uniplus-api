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
    IReadOnlyList<ProdutoDaEtapaDto> Produtos);

/// <summary>Projeção de leitura de <c>ProdutoDaEtapa</c>.</summary>
public sealed record ProdutoDaEtapaDto(Guid Id, string AtoCodigo, PapelProdutoFase? Papel);

/// <summary>Cópia por valor do tipo de etapa, projetada para leitura (ADR-0061).</summary>
public sealed record TipoEtapaSnapshotDto(Guid OrigemId, string Codigo, string Nome);
