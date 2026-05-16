namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// DTO de leitura de <c>ObrigatoriedadeLegal</c> exposto pela camada API.
/// Reflete o estado atual da regra após qualquer mutação in-place (ADR-0058
/// Emenda 1) — a reconstrução de versões anteriores é via
/// <c>obrigatoriedade_legal_historico</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual exibido para auditoria — pode incluir DOI, "
        + "URN ou identificadores não-HTTP. Espelha a propriedade homônima da entidade.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Construtor do record propaga o tipo string do payload — ver justificativa acima.")]
public sealed record ObrigatoriedadeLegalDto(
    Guid Id,
    string TipoEditalCodigo,
    CategoriaObrigatoriedade Categoria,
    string RegraCodigo,
    PredicadoObrigatoriedade Predicado,
    string DescricaoHumana,
    string BaseLegal,
    string? AtoNormativoUrl,
    string? PortariaInternaCodigo,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string Hash,
    string? Proprietario,
    IReadOnlyList<string> AreasDeInteresse,
    bool IsDeleted)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1) — opt-in, populado pelo
    /// <c>IResourceLinksBuilder&lt;ObrigatoriedadeLegalDto&gt;</c> no boundary
    /// HTTP para respostas single. Coleções não carregam — navegação via
    /// header <c>Link</c> (ADR-0026).
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
