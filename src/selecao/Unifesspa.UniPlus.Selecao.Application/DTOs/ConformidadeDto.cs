namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

/// <summary>
/// Resultado da avaliação de conformidade de um edital — payload do
/// <c>GET /api/selecao/editais/{id}/conformidade</c> (estado atual) e do
/// <c>GET /api/selecao/editais/{id}/conformidade-historica</c> (snapshot
/// publicado, lido de <c>EditalGovernanceSnapshot.RegrasJson</c>).
/// </summary>
public sealed record ConformidadeDto(
    Guid EditalId,
    IReadOnlyList<RegraAvaliadaDto> Regras)
{
    /// <summary>
    /// Links HATEOAS Level 1 — sempre carregam <c>self</c> apontando para o
    /// próprio endpoint que produziu a resposta.
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>
/// Item individual da lista de regras avaliadas. Reflete o que o
/// <c>ValidadorConformidadeEdital</c> produz: code da regra, aprovação,
/// citação legal, descrição humana, hash do snapshot.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual de citação normativa "
        + "(DOI, URN, IRI) — preserva fidelidade do valor original informado pelo admin.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Construtor do record propaga o tipo string do payload — ver justificativa acima.")]
public sealed record RegraAvaliadaDto(
    Guid RegraId,
    string RegraCodigo,
    bool Aprovada,
    string BaseLegal,
    string? PortariaInternaCodigo,
    string? AtoNormativoUrl,
    string DescricaoHumana,
    string Hash,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim);
