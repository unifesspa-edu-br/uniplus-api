namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>LocalOferta</c>. Agrupa a cidade
/// (<see cref="CidadeReferenciaDto"/>) e o endereço estruturado
/// (<see cref="EnderecoGeoDto"/>, opcional) como sub-objetos aninhados (ADR-0096,
/// CA-02). O <see cref="Tipo"/> serializa como nome camelCase
/// (JsonStringEnumConverter). Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029).
/// </summary>
public sealed record LocalOfertaDto(
    Guid Id,
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    CidadeReferenciaDto Cidade,
    EnderecoGeoDto? Endereco,
    string? CodigoEmec,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
