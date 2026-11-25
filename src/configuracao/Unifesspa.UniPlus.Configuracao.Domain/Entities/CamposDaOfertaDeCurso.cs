namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>A declaração de uma oferta de curso depois de conferida: códigos já resolvidos, textos sem espaço nas pontas.</summary>
internal sealed record CamposDaOfertaDeCurso(
    string Codigo,
    ProgramaDeOferta ProgramaDeOferta,
    FormatoPedagogico FormatoPedagogico,
    RegimeDeFuncionamento RegimeDeFuncionamento,
    RegimeDeTurno RegimeDeTurno,
    IReadOnlyList<TurnoOferta> Turnos,
    string? CodigoEmec,
    string? CodigoNoSistemaAcademico,
    int? VagasAnuaisAutorizadas,
    string? BaseLegal,
    string? AtoAutorizacaoMec);
