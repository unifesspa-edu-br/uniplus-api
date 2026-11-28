namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <remarks>
/// <c>Nome</c>/<c>Slug</c>/<c>Sigla</c>/<c>Codigo</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem valor default, para o schema OpenAPI
/// continuar listando-os como obrigatórios; nulos, para o campo ausente
/// escapar do <c>[ApiController]</c> e chegar à validação de domínio. <c>UnidadeAcademica</c> e
/// <c>VigenciaInicio</c> são <c>bool?</c>/<c>DateOnly?</c> sem valor padrão, pelo mesmo motivo: ausentes ou nulos
/// são recusados com 422, em vez de virarem <c>false</c> e o ano 1 em silêncio.
/// </remarks>
public sealed record CriarUnidadeCommand(
    string? Nome,
    string? Alias,
    string? Slug,
    string? Sigla,
    string? Codigo,
    Guid? UnidadeSuperiorId,
    [property: AceitaCodigosDe<TipoUnidade>] string? Tipo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? UnidadeAcademica,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim,
    string? CidadeCodigoIbge = null,
    string? CidadeNome = null,
    string? CidadeUf = null) : ICommand<Result<Guid>>;
