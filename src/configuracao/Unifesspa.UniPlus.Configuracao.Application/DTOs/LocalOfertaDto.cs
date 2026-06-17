namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>LocalOferta</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O <see cref="Tipo"/> serializa como nome camelCase
/// (JsonStringEnumConverter), mesmo contrato de entrada do command.
/// </summary>
public sealed record LocalOfertaDto(
    Guid Id,
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    string? CidadeOrigem,
    DateTimeOffset? CidadeDisplayAtualizadoEm,
    string? Endereco,
    string? CodigoEmec,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
