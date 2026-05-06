namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

public sealed record EditalDto(
    Guid Id,
    string NumeroEdital,
    string Titulo,
    string TipoProcesso,
    string Status,
    int MaximoOpcoesCurso,
    bool BonusRegionalHabilitado,
    DateTimeOffset CriadoEm)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1) — opt-in, populado pelo
    /// <c>IResourceLinksBuilder&lt;EditalDto&gt;</c> no boundary HTTP da API
    /// para respostas de recurso single (ex.: <c>GET /api/editais/{id}</c>).
    /// Coleções não carregam <c>_links</c> — navegação vai por header
    /// <c>Link</c> (ADR-0026). Vedados action links (<c>publicar</c> etc.)
    /// per ADR-0029.
    /// </summary>
    [JsonPropertyName("_links")]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
