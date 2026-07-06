namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma modalidade selecionada (Story #773).</summary>
public sealed record ModalidadeSelecionadaDto(
    Guid Id,
    Guid ModalidadeOrigemId,
    string Codigo,
    string? Descricao,
    string NaturezaLegal,
    string ComposicaoVagas,
    string? ComposicaoOrigemCodigo,
    string RegraRemanejamento,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    IReadOnlyList<string> CriteriosCumulativos,
    string? AcaoQuandoIndeferido,
    string BaseLegal);

/// <summary>Projeção de leitura de uma referência a regra do rol_de_regras (Story #772).</summary>
public sealed record ReferenciaRegraDto(string Codigo, string Versao, string Hash);

/// <summary>Projeção de leitura do snapshot de referência de reserva demográfica (Story #773).</summary>
public sealed record ReferenciaReservaDemograficaSnapshotDto(
    Guid OrigemId,
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal);

/// <summary>
/// Projeção de leitura de <c>ConfiguracaoDistribuicaoVagas</c> (Story #773). O
/// <c>QuadroDeVagas</c> (quantidade calculada por modalidade) não aparece aqui
/// — é output derivado de um motor futuro, não desta configuração.
/// </summary>
public sealed record ConfiguracaoDistribuicaoVagasDto(
    Guid Id,
    Guid OfertaCursoOrigemId,
    int VoBase,
    decimal Pr,
    ReferenciaRegraDto RegraDistribuicao,
    ReferenciaReservaDemograficaSnapshotDto? ReferenciaDemografica,
    IReadOnlyList<ModalidadeSelecionadaDto> Modalidades);
