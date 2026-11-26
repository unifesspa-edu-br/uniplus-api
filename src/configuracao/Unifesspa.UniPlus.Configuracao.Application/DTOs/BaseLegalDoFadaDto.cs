namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed record BaseLegalDoFadaDto(
    Guid Id,
    string Codigo,
    CodigoComRotulo<TipoInstrumentoNormativo> TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<MunicipioDaBaseLegalDoFadaDto> Municipios,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

public sealed record MunicipioDaBaseLegalDoFadaDto(
    string CodigoIbge,
    string Nome,
    string Uf);
