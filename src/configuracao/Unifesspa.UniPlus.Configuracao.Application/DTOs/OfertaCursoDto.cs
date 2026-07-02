namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>OfertaCurso</c>. A unidade ofertante é um
/// sub-objeto aninhado (<see cref="UnidadeOfertanteDto"/>, snapshot-copy
/// ADR-0061). Os enums são expostos como tokens UPPER_SNAKE do contrato
/// (<c>programaDeOferta</c>/<c>formatoPedagogico</c> obrigatórios;
/// <c>turno</c> nulo quando a oferta não declara turno). Suporta HATEOAS
/// Level 1 via <c>_links</c> (ADR-0029).
/// </summary>
public sealed record OfertaCursoDto(
    Guid Id,
    Guid CursoId,
    Guid LocalOfertaId,
    UnidadeOfertanteDto UnidadeOfertante,
    string ProgramaDeOferta,
    string FormatoPedagogico,
    string? Turno,
    string? EMecCodigo,
    string? CodigoSga,
    int? VagasAnuaisAutorizadas,
    string? BaseLegal,
    string? AtoAutorizacaoMec,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
