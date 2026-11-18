namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>A declaração de uma oferta de curso depois de conferida: tokens já resolvidos, textos sem espaço nas pontas.</summary>
internal sealed record CamposDaOfertaDeCurso(
    string Codigo,
    ProgramaDeOferta ProgramaDeOferta,
    FormatoPedagogico FormatoPedagogico,
    RegimeDeFuncionamento RegimeDeFuncionamento,
    RegimeDeTurno RegimeDeTurno,
    IReadOnlyList<TurnoOferta> Turnos,
    string? EMecCodigo,
    string? CodigoSga,
    int? VagasAnuaisAutorizadas,
    string? BaseLegal,
    string? AtoAutorizacaoMec);
