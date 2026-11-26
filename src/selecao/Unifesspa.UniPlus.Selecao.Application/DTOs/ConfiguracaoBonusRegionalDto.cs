namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura do bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774), com o snapshot congelado da Base Legal (Story #1466).</summary>
public sealed record ConfiguracaoBonusRegionalDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    decimal Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalCongeladaDto BaseLegal);

/// <summary>O snapshot congelado da Base Legal do bônus regional: o instrumento normativo e os municípios que ele beneficia.</summary>
/// <param name="TipoInstrumento">O código do tipo do instrumento normativo, do vocabulário <c>tipos-instrumento-normativo</c> da Configuração.</param>
public sealed record BaseLegalCongeladaDto(
    Guid Id,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<ConfiguracaoBonusRegionalMunicipioDto> Municipios);

/// <summary>Um município do snapshot congelado do bônus regional.</summary>
public sealed record ConfiguracaoBonusRegionalMunicipioDto(
    string CodigoIbge,
    string Nome,
    string Uf);
