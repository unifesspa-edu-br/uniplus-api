namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Um dia não útil do dataset, projetado para resposta HTTP (token de
/// <c>Abrangencia</c> UPPER_SNAKE). Dias municipais carregam o snapshot de código
/// IBGE, nome e UF; <c>Uf</c> permanece reservada à abrangência estadual.
/// </summary>
public sealed record DiaNaoUtilDto(
    Guid Id,
    string Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    string? Uf,
    DateOnly Data,
    string Descricao);
