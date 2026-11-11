namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// DTO de leitura de um motivo do cadastro único de motivos de decisão (UNI-REQ-0283). Tipo e
/// sentido saem como código textual canônico, e não como número do enum: o contrato não muda de
/// significado porque alguém reordenou a declaração no C#.
/// </summary>
public sealed record MotivoDeDecisaoDto(
    Guid Id,
    string Tipo,
    string Codigo,
    string Descricao,
    string? Subclassificacao,
    string Sentido,
    bool Ativo)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1), populados no boundary HTTP para respostas single.
    /// Coleções não carregam — navegação via header <c>Link</c> (ADR-0026).
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
