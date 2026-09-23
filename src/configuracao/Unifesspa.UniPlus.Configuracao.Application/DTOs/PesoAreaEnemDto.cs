namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>PesoAreaEnem</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O grupo de área vem com código e rótulo; as áreas, na ordem
/// canônica, com código e rótulo oficial postos pelo sistema.
/// </summary>
public sealed record PesoAreaEnemDto(
    Guid Id,
    string Resolucao,
    GrupoAreaEnemDto GrupoCurso,
    IReadOnlyList<PesoAreaEnemAreaDto> Areas,
    string BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}

/// <summary>Peso e corte de uma área numa linha de Pesos por Área.</summary>
/// <param name="Codigo">Código da área, sem abreviação e sem acento.</param>
/// <param name="Rotulo">Rótulo oficial da área, posto pelo sistema.</param>
/// <param name="Peso">Peso da área na média.</param>
/// <param name="Corte">Nota mínima da área (0–1000), ou <see langword="null"/> sem corte.</param>
public sealed record PesoAreaEnemAreaDto(
    string Codigo,
    string Rotulo,
    decimal Peso,
    decimal? Corte);

/// <summary>
/// Uma das cinco áreas do cadastro de Pesos por Área, com o rótulo oficial. Existe para o
/// formulário de cadastro montar as colunas sem manter cópia das áreas.
/// </summary>
/// <param name="Codigo">Código da área, o mesmo aceito em <c>areas[].codigo</c> na gravação.</param>
/// <param name="Rotulo">Rótulo oficial da área.</param>
public sealed record AreaPesoAreaEnemDto(
    string Codigo,
    string Rotulo);
