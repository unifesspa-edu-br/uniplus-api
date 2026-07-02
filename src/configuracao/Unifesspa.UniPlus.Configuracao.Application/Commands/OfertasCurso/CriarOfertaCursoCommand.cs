namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma oferta de curso — a instância regulatória que liga um curso vivo a
/// um local de oferta vivo e à unidade ofertante (story #588, issue #749,
/// ADR-0066). A unidade chega como <paramref name="UnidadeOfertanteOrigemId"/>:
/// o handler resolve a Unidade viva via <c>IUnidadeReader</c> (ADR-0056) e
/// congela sigla/nome/tipo por snapshot-copy (ADR-0061) — o payload nunca traz
/// o snapshot pronto. Enums como tokens UPPER_SNAKE: programa obrigatório;
/// formato pedagógico com default PRESENCIAL quando ausente; turno opcional.
/// A base legal é obrigatória quando o programa não é REGULAR (guard de
/// domínio). O ator de auditoria (<c>created_by</c>) é carimbado server-side
/// via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarOfertaCursoCommand(
    Guid CursoId,
    Guid LocalOfertaId,
    Guid UnidadeOfertanteOrigemId,
    string ProgramaDeOferta,
    string? FormatoPedagogico = null,
    string? Turno = null,
    string? EMecCodigo = null,
    string? CodigoSga = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null) : ICommand<Result<Guid>>;
