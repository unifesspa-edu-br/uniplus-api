namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>TermoConsentimento</c> na listagem — sem as
/// versões promovidas (a listagem não carrega a coleção filha; use
/// <c>ObterPorId</c> para o termo completo). Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record TermoConsentimentoResumoDto(
    Guid Id,
    string Nome,
    string FormaAceiteRascunho,
    bool Revisado,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
