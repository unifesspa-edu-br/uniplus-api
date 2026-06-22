namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para a <c>Instituicao</c> singleton. Exposto pelos
/// endpoints do controller; suporta HATEOAS Level 1 via <c>_links</c>.
/// </summary>
public sealed record InstituicaoDto(
    Guid Id,
    string CodigoEmec,
    string Nome,
    string Sigla,
    string OrganizacaoAcademica,
    string CategoriaAdministrativa,
    string? Cnpj,
    string? Mantenedora,
    string? CodigoMantenedoraEmec,
    string? Situacao,
    string? AtoCredenciamento,
    string? AtoRecredenciamento,
    string? ConceitoInstitucional,
    string? Igc,
    string? Website,
    CidadeReferenciaDto? Cidade,
    EnderecoGeoDto? Endereco,
    Guid? UnidadeRaizId,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
