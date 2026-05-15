namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Representação HTTP de <c>AreaOrganizacional</c>. <see cref="Codigo"/> é
/// serializado como string em uppercase (formato canônico do <c>AreaCodigo</c>).
/// <see cref="Links"/> populado pelo boundary HTTP (HATEOAS L1, ADR-0029/0049).
/// </summary>
public sealed record AreaOrganizacionalDto(
    Guid Id,
    string Codigo,
    string Nome,
    string Tipo,
    string Descricao,
    string AdrReferenceCode,
    DateTimeOffset CriadoEm)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1) — opt-in, populado pelo
    /// <c>IResourceLinksBuilder&lt;AreaOrganizacionalDto&gt;</c> no boundary
    /// HTTP da API para respostas de recurso single. <see cref="JsonIgnoreCondition.WhenWritingNull"/>
    /// suprime o campo em coleções (ADR-0026 navegação vai por header <c>Link</c>).
    /// Vedados action links per ADR-0029.
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
