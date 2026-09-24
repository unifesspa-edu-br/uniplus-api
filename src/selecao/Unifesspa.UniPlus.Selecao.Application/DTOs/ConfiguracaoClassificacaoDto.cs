namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma regra de eliminação (Story #775).</summary>
public sealed record RegraEliminacaoDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    Guid? EtapaRef,
    decimal? NotaMinima,
    decimal? Minimo);

/// <summary>Peso e corte de uma área no quadro de pesos por área congelado na classificação.</summary>
/// <param name="Codigo">Código da área, sem abreviação e sem acento.</param>
/// <param name="Rotulo">Rótulo oficial da área.</param>
/// <param name="Peso">Peso da área na média do grupo.</param>
/// <param name="Corte">Nota mínima da área, ou <see langword="null"/> quando a área não tem corte.</param>
public sealed record AreaPesoAreaEnemCongeladaDto(
    string Codigo,
    string Rotulo,
    decimal Peso,
    decimal? Corte);

/// <summary>Um grupo de área do quadro de pesos por área congelado na classificação.</summary>
/// <param name="GrupoAreaEnem">O grupo de área, com código e rótulo.</param>
/// <param name="BaseLegal">Dispositivo legal que fundamenta os pesos do grupo.</param>
/// <param name="Areas">As áreas do grupo, ordenadas pelo código.</param>
public sealed record GrupoPesoAreaEnemCongeladoDto(
    GrupoAreaEnemSnapshotDto GrupoAreaEnem,
    string BaseLegal,
    IReadOnlyList<AreaPesoAreaEnemCongeladaDto> Areas);

/// <summary>
/// Projeção de leitura de <c>ConfiguracaoClassificacao</c> (Story #775, 15º
/// bloco canônico). Bônus e desempate não aparecem aqui — já são dimensões
/// próprias do agregado (Story #774).
/// </summary>
/// <param name="ResolucaoPesoAreaEnem">Resolução de Pesos por Área declarada; nula fora da classificação baseada em ENEM com cálculo local.</param>
/// <param name="QuadroPesoAreaEnem">Cópia por valor da resolução declarada, um item por grupo de área, ordenada pelo código do grupo; vazia sem resolução.</param>
public sealed record ConfiguracaoClassificacaoDto(
    Guid Id,
    ReferenciaRegraDto RegraCalculo,
    ReferenciaRegraDto? RegraArredondamento,
    int? CasasArredondamento,
    ReferenciaRegraDto RegraOrdemAlocacao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoDto> RegrasEliminacao,
    bool ConcorrenciaDuplaAplicavel,
    bool BaseadoEmEnem,
    string? ResolucaoPesoAreaEnem,
    IReadOnlyList<GrupoPesoAreaEnemCongeladoDto> QuadroPesoAreaEnem);
