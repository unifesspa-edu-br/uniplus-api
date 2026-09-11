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

/// <summary>Projeção de leitura do bônus regional (RN05, Story #774), com o snapshot congelado da Base Legal (Story #1466).</summary>
public sealed record ConfiguracaoBonusRegionalDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    decimal Fator,
    decimal? Teto,
    Guid BaseLegalBonusRegionalId,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<ConfiguracaoBonusRegionalMunicipioDto> Municipios);

/// <summary>Um município do snapshot congelado do bônus regional.</summary>
public sealed record ConfiguracaoBonusRegionalMunicipioDto(
    string CodigoIbge,
    string Nome,
    string Uf);
