namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>Representação pública de um tipo de processo ativo.</summary>
public sealed record TipoProcessoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool Ativo,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
