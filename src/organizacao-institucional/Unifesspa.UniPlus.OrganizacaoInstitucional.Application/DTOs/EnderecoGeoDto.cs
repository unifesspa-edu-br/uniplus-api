namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Endereço estruturado como referência ao Geo via CEP (ADR-0096), exposto como
/// sub-objeto aninhado no contrato HTTP (CA-02). <c>Cep</c>, <c>Cidade</c>,
/// <c>NivelResolucao</c> e <c>Origem</c> são sempre presentes; logradouro,
/// número, complemento, bairro, distrito e coordenada acompanham a resolução do
/// CEP (podem ser nulos em resolução rasa). Mantido byte-equivalente à cópia do
/// módulo Configuração (ADR-0035).
/// </summary>
public sealed record EnderecoGeoDto(
    string Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Distrito,
    CidadeReferenciaDto Cidade,
    decimal? Latitude,
    decimal? Longitude,
    string NivelResolucao,
    string Origem,
    DateTimeOffset? DisplayAtualizadoEm);
