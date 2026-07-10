namespace Unifesspa.UniPlus.Publicacoes.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta para <c>TipoAtoPublicado</c>. Os três atributos de consequência
/// são expostos como dados — quem os consome copia-os por valor no ato publicado,
/// nunca ramifica comportamento por código de tipo (ADR-0103).
/// </summary>
/// <remarks>
/// <para>A janela de vigência é semiaberta: <c>VigenciaFim</c> é o primeiro dia em
/// que esta versão já não vale, e é nula enquanto a vigência é aberta.</para>
/// <para>Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029). A autoria
/// (<c>created_by</c>/<c>updated_by</c>) não é exposta — é dado de auditoria
/// interna.</para>
/// </remarks>
public sealed record TipoAtoPublicadoDto(
    Guid Id,
    string Codigo,
    string Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
