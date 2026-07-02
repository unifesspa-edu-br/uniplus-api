namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza os atributos editáveis de uma oferta de curso: programa, formato
/// pedagógico, turno, códigos (e-MEC / SGA), teto de vagas, base legal e ato de
/// autorização. <c>CursoId</c>, <c>LocalOfertaId</c> e a unidade ofertante
/// (snapshot-copy, ADR-0061) são <b>imutáveis</b> — mudar curso×local×unidade
/// caracteriza outra oferta; este comando não os aceita. O guard condicional da
/// base legal é revalidado na transição (Regular→Parfor sem base é rejeitado).
/// O ator (<c>updated_by</c>) é carimbado server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarOfertaCursoCommand(
    Guid Id,
    string ProgramaDeOferta,
    string? FormatoPedagogico = null,
    string? Turno = null,
    string? EMecCodigo = null,
    string? CodigoSga = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null) : ICommand<Result>;
