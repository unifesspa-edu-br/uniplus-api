namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// DTO de resposta HTTP para <c>OfertaCurso</c>. A unidade ofertante é um
/// sub-objeto aninhado (<see cref="UnidadeOfertanteDto"/>, cópia dos dados
/// ADR-0061). O programa, o formato, os regimes e os turnos saem como código e rótulo
/// (<c>programaDeOferta</c>, <c>formatoPedagogico</c>, <c>regimeDeFuncionamento</c>, <c>regimeDeTurno</c> e <c>turnos</c>).
/// <c>regimeDeFuncionamento</c> é dimensão própria, independente do regime de
/// turno (UNI-REQ-0138). <c>turnos</c> traz de um a dois turnos, conforme o
/// regime de turno, em ordem canônica (matutino, vespertino, noturno). Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record OfertaCursoDto(
    Guid Id,
    string Codigo,
    Guid CursoId,
    Guid LocalOfertaId,
    UnidadeOfertanteDto UnidadeOfertante,
    CodigoComRotulo<ProgramaDeOferta> ProgramaDeOferta,
    CodigoComRotulo<FormatoPedagogico> FormatoPedagogico,
    CodigoComRotulo<RegimeDeFuncionamento> RegimeDeFuncionamento,
    CodigoComRotulo<RegimeDeTurno> RegimeDeTurno,
    IReadOnlyList<CodigoComRotulo<TurnoOferta>> Turnos,
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
