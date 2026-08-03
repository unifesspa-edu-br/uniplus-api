namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>TermoConsentimento</c>, com a lista completa de
/// versões promovidas — usado pelo <c>ObterPorId</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record TermoConsentimentoDto(
    Guid Id,
    string Nome,
    string? TextoRascunho,
    string? BaseLegalRascunho,
    string FormaAceiteRascunho,
    bool Revisado,
    string? RevisadoPor,
    DateTimeOffset? RevisadoEm,
    IReadOnlyList<TermoConsentimentoVersaoDto> Versoes,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
