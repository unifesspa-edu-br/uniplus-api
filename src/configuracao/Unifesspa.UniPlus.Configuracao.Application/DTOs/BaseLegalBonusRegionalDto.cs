namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

public sealed record BaseLegalBonusRegionalDto(
    Guid Id,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<BaseLegalBonusRegionalMunicipioDto> Municipios,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

public sealed record BaseLegalBonusRegionalMunicipioDto(
    string CodigoIbge,
    string Nome,
    string Uf);
