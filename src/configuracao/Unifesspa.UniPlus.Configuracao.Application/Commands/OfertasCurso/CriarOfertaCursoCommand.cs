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
/// formato pedagógico com default PRESENCIAL quando ausente; regime de turno e
/// turnos obrigatórios, conferidos entre si (REGULAR exige um turno; INTEGRAL,
/// dois distintos). A base legal é obrigatória quando o programa não é REGULAR
/// (guard de domínio). O ator de auditoria (<c>created_by</c>) é carimbado server-side
/// via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>ProgramaDeOferta</c> e <c>RegimeDeTurno</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem valor default, para o schema OpenAPI continuar
/// listando-os como obrigatórios;
/// nulo, para o campo ausente escapar do <c>[ApiController]</c> e chegar à
/// validação de domínio — mesmo padrão de <c>Curso.Codigo</c> e
/// <c>Modalidade.Codigo</c>.
/// </remarks>
public sealed record CriarOfertaCursoCommand(
    Guid CursoId,
    Guid LocalOfertaId,
    Guid UnidadeOfertanteOrigemId,
    string? ProgramaDeOferta,
    string? RegimeDeTurno,
    IReadOnlyList<string?>? Turnos,
    string? FormatoPedagogico = null,
    string? EMecCodigo = null,
    string? CodigoSga = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null) : ICommand<Result<Guid>>;
