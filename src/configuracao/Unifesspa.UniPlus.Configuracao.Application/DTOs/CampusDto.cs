namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>Campus</c>. Agrupa a cidade
/// (<see cref="CidadeReferenciaDto"/>) e o endereço estruturado
/// (<see cref="EnderecoGeoDto"/>, opcional) como sub-objetos aninhados (ADR-0096,
/// CA-02). Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029).
/// </summary>
public sealed record CampusDto(
    Guid Id,
    string Sigla,
    string Nome,
    CidadeReferenciaDto Cidade,
    EnderecoGeoDto? Endereco,
    string? CodigoEmec,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
