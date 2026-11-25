namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma oferta de curso — a instância regulatória que liga um curso vivo a
/// um local de oferta vivo e à unidade ofertante (ADR-0066). A unidade chega como <paramref name="UnidadeOfertanteOrigemId"/>:
/// o handler resolve a Unidade viva via <c>IUnidadeReader</c> (ADR-0056) e
/// copia sigla, nome e tipo (ADR-0061) — o payload nunca traz
/// a cópia pronta. O <c>Codigo</c> é informado pela gestão, obrigatório e único entre as ofertas vivas. Os enums chegam como código de contrato: programa obrigatório;
/// formato pedagógico com default PRESENCIAL quando ausente; regime de
/// funcionamento, regime de turno e turnos obrigatórios, conferidos entre si
/// (REGULAR exige um turno; INTEGRAL, dois distintos; INTENSIVO exige INTEGRAL). A base legal é obrigatória quando o programa não é REGULAR
/// (guard de domínio). O ator de auditoria (<c>created_by</c>) é carimbado server-side
/// via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>ProgramaDeOferta</c>, <c>RegimeDeFuncionamento</c> e <c>RegimeDeTurno</c>
/// são <c>string?</c>, não
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
    string? Codigo,
    [property: AceitaCodigosDe<ProgramaDeOferta>] string? ProgramaDeOferta,
    [property: AceitaCodigosDe<RegimeDeFuncionamento>] string? RegimeDeFuncionamento,
    [property: AceitaCodigosDe<RegimeDeTurno>] string? RegimeDeTurno,
    [property: AceitaCodigosDe<TurnoOferta>] IReadOnlyList<string?>? Turnos,
    [property: AceitaCodigosDe<FormatoPedagogico>] string? FormatoPedagogico = null,
    string? CodigoEmec = null,
    string? CodigoNoSistemaAcademico = null,
    int? VagasAnuaisAutorizadas = null,
    string? BaseLegal = null,
    string? AtoAutorizacaoMec = null) : ICommand<Result<Guid>>
{
    /// <summary>O que a gestão declara da oferta, sem o curso, o local e a unidade, que não mudam depois de criada.</summary>
    public DeclaracaoDaOfertaDeCurso Declaracao() => new(
        Codigo, ProgramaDeOferta, FormatoPedagogico, RegimeDeFuncionamento, RegimeDeTurno, Turnos,
        CodigoEmec, CodigoNoSistemaAcademico, VagasAnuaisAutorizadas, BaseLegal, AtoAutorizacaoMec);
}
