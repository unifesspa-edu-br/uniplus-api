namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura do Fator de Atenuação das Desigualdades de Acesso (FADA, UNI-REQ-0015), com o snapshot congelado da Base Legal.</summary>
public sealed record ConfiguracaoDoFadaDto(
    Guid Id,
    ReferenciaRegraDto Regra,
    decimal Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalCongeladaDto BaseLegal);

/// <summary>O snapshot congelado da Base Legal do FADA: o instrumento normativo e os municípios que ele beneficia.</summary>
/// <param name="TipoInstrumento">O código do tipo do instrumento normativo, do vocabulário <c>tipos-instrumento-normativo</c> da Configuração.</param>
public sealed record BaseLegalCongeladaDto(
    Guid Id,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<MunicipioDoFadaDto> Municipios);

/// <summary>Um município do snapshot congelado do FADA.</summary>
public sealed record MunicipioDoFadaDto(
    string CodigoIbge,
    string Nome,
    string Uf);
