namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma regra de eliminação (Story #775).</summary>
public sealed record RegraEliminacaoDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    Guid? EtapaRef,
    decimal? NotaMinima,
    decimal? Minimo);

/// <summary>
/// Projeção de leitura de <c>ConfiguracaoClassificacao</c> (Story #775, 15º
/// bloco canônico). Bônus e desempate não aparecem aqui — já são dimensões
/// próprias do agregado (Story #774).
/// </summary>
public sealed record ConfiguracaoClassificacaoDto(
    Guid Id,
    ReferenciaRegraDto RegraCalculo,
    ReferenciaRegraDto? RegraArredondamento,
    int? CasasArredondamento,
    ReferenciaRegraDto RegraOrdemAlocacao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoDto> RegrasEliminacao,
    bool ConcorrenciaDuplaAplicavel);
