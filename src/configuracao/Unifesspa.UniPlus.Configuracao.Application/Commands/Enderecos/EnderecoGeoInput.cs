namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;

/// <summary>
/// Endereço estruturado no payload de entrada (ADR-0096), espelhando o
/// <c>CepResolvidoDto</c> do Geo composto pelo front. Opcional na entidade; quando
/// presente, exige CEP + cidade (validados por
/// <c>ReferenciaEnderecoGeo</c>). O <c>displayAtualizadoEm</c> não viaja — é
/// carimbado server-side pelo handler.
/// </summary>
public sealed record EnderecoGeoInput(
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Distrito,
    CidadeReferenciaInput? Cidade,
    decimal? Latitude,
    decimal? Longitude,
    string? NivelResolucao,
    string? Origem);
