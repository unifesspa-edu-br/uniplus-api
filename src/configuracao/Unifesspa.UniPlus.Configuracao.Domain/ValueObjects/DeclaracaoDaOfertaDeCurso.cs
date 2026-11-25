namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O que a gestão declara de uma oferta de curso, em criação e em edição: o código, os regimes, os turnos e os atributos
/// regulatórios, ainda como chegam (códigos de contrato e texto livre). Curso, local e unidade ofertante não fazem
/// parte da declaração porque nunca mudam depois da criação.
/// </summary>
/// <param name="Codigo">Código informado pela gestão, obrigatório.</param>
/// <param name="ProgramaDeOferta">Programa de oferta, obrigatório.</param>
/// <param name="FormatoPedagogico">Formato pedagógico; ausente vale PRESENCIAL.</param>
/// <param name="RegimeDeFuncionamento">Regime de funcionamento, obrigatório.</param>
/// <param name="RegimeDeTurno">Regime de turno, obrigatório.</param>
/// <param name="Turnos">Turnos ocupados, obrigatórios e conferidos contra o regime de turno.</param>
/// <param name="CodigoEmec">Código e-MEC da oferta, opcional.</param>
/// <param name="CodigoNoSistemaAcademico">Código no sistema acadêmico, opcional.</param>
/// <param name="VagasAnuaisAutorizadas">Teto de vagas autorizadas no e-MEC, opcional.</param>
/// <param name="BaseLegal">Base legal, obrigatória quando o programa não é regular.</param>
/// <param name="AtoAutorizacaoMec">Ato de autorização do MEC, opcional.</param>
public sealed record DeclaracaoDaOfertaDeCurso(
    string? Codigo,
    string? ProgramaDeOferta,
    string? FormatoPedagogico,
    string? RegimeDeFuncionamento,
    string? RegimeDeTurno,
    IReadOnlyList<string?>? Turnos,
    string? CodigoEmec = null,
    string? CodigoNoSistemaAcademico = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null);
