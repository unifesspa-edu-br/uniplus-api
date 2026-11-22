namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>FaseCanonica</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O <c>OrigemData</c> é exposto como código e rótulo
/// e a <c>UnidadeSugerida</c> como a cópia congelada de id, sigla e nome.
/// </summary>
/// <param name="AgrupaEtapas">Sinalizador gravado na fase: se ela agrupa Etapas pontuadas.</param>
/// <param name="PermiteComplementacao">Sinalizador gravado na fase: se ela aceita complementação documental.</param>
/// <param name="AdmiteAgruparEtapas">Se o código da fase admite agrupar Etapas (só a avaliação); calculado pelo catálogo, não gravado.</param>
/// <param name="AdmiteComplementacao">Se o código da fase admite complementação documental por lei; calculado pelo catálogo, não gravado.</param>
public sealed record FaseCanonicaDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool AdmiteAgruparEtapas,
    bool AdmiteComplementacao,
    string? BaseLegal,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    CodigoComRotulo<OrigemDataFase> OrigemData,
    UnidadeSugeridaDto? UnidadeSugerida,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>Cópia congelada da unidade sugerida para a fase canônica (ADR-0061).</summary>
public sealed record UnidadeSugeridaDto(Guid OrigemId, string Sigla, string Nome);
