namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>Um dia não útil do dataset, projetado para resposta HTTP (token de <c>Abrangencia</c> UPPER_SNAKE).</summary>
public sealed record DiaNaoUtilDto(
    Guid Id,
    string Abrangencia,
    string? MunicipioIbge,
    string? Uf,
    DateOnly Data,
    string Descricao);
