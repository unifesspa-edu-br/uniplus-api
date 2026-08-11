namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

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
    string Carater,
    TipoEtapaSnapshotDto TipoEtapa,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem);

/// <summary>Cópia por valor do tipo de etapa, projetada para leitura (ADR-0061).</summary>
public sealed record TipoEtapaSnapshotDto(Guid OrigemId, string Codigo, string Nome);
