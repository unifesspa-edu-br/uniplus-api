namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza os atributos editáveis de uma oferta de curso: programa, formato
/// pedagógico, regime de turno e turnos, códigos (e-MEC / SGA), teto de vagas,
/// base legal e ato de autorização. <c>CursoId</c>, <c>LocalOfertaId</c> e a unidade ofertante
/// (snapshot-copy, ADR-0061) são <b>imutáveis</b> — mudar curso×local×unidade
/// caracteriza outra oferta; este comando não os aceita. O guard condicional da
/// base legal é revalidado na transição (Regular→Parfor sem base é rejeitado).
/// O ator (<c>updated_by</c>) é carimbado server-side via <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// <c>ProgramaDeOferta</c> e <c>RegimeDeTurno</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem valor default, para o schema OpenAPI continuar
/// listando-os como obrigatórios;
/// nulo, para o campo ausente escapar do <c>[ApiController]</c> e chegar à
/// validação de domínio.
/// </remarks>
public sealed record AtualizarOfertaCursoCommand(
    Guid Id,
    string? ProgramaDeOferta,
    string? RegimeDeTurno,
    IReadOnlyList<string?>? Turnos,
    string? FormatoPedagogico = null,
    string? EMecCodigo = null,
    string? CodigoSga = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null) : ICommand<Result>;
