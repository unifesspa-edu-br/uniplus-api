namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>FaseCanonica</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O <c>OrigemData</c> é exposto como token
/// canônico UPPER_SNAKE e a <c>UnidadeSugerida</c> como a cópia congelada de id, sigla e nome.
/// </summary>
public sealed record FaseCanonicaDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    string? BaseLegal,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    string OrigemData,
    UnidadeSugeridaDto? UnidadeSugerida,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>Cópia congelada da unidade sugerida para a fase canônica (ADR-0061).</summary>
public sealed record UnidadeSugeridaDto(Guid OrigemId, string Sigla, string Nome);
