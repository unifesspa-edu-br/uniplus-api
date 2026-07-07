namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Projeção de leitura de um critério de desempate (Story #774). Os args
/// além de <see cref="Regra"/> refletem a mesma forma flat de
/// <c>CriterioDesempateInput</c> — apenas o(s) relevante(s) para o código da
/// regra referenciada vem(êm) preenchido(s).
/// </summary>
public sealed record CriterioDesempateDto(
    Guid Id,
    int Ordem,
    ReferenciaRegraDto Regra,
    Guid? EtapaRef,
    int? IdadeMinima,
    string? Fato,
    string? Operador,
    string? Valor);

/// <summary>Projeção de leitura do bônus regional (RN05, Story #774).</summary>
public sealed record ConfiguracaoBonusRegionalDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    decimal Fator,
    decimal? Teto,
    string? MunicipioConvenio,
    string? BaseLegal);
