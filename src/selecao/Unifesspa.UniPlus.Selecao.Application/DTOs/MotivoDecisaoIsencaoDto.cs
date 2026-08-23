namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// DTO de leitura de um motivo de decisão de isenção (UNI-REQ-0120).
/// </summary>
/// <remarks>
/// Fundamento e resultado permitido saem como código textual canônico, e não
/// como número do enum: o contrato público não deve mudar de significado
/// porque alguém reordenou a declaração no C#.
/// </remarks>
public sealed record MotivoDecisaoIsencaoDto(
    Guid Id,
    string Codigo,
    string Descricao,
    string Fundamento,
    string ResultadoPermitido,
    bool Ativo)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1) — opt-in, populado pelo
    /// <c>IResourceLinksBuilder&lt;MotivoDecisaoIsencaoDto&gt;</c> no boundary
    /// HTTP para respostas single. Coleções não carregam — navegação via header
    /// <c>Link</c> (ADR-0026).
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
