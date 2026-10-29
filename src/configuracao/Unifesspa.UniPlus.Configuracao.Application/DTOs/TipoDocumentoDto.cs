namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>TipoDocumento</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). A categoria é exposta como <c>string</c> (token
/// canônico UPPER_SNAKE); o tamanho máximo como número (MB). A classificação de proteção e a
/// hipótese legal seguem os tokens UPPER_SNAKE do vocabulário dos fatos.
/// </summary>
public sealed record TipoDocumentoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Categoria,
    string? FormatosAceitos,
    int? TamanhoMaximoMb,
    string? TipoEquivalente,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
