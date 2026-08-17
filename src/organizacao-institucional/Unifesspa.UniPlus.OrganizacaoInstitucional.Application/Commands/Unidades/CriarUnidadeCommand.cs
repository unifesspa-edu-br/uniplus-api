namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <remarks>
/// <c>Nome</c>/<c>Slug</c>/<c>Sigla</c>/<c>Codigo</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem valor default, para o schema OpenAPI
/// continuar listando-os como obrigatórios; nulos, para o campo ausente
/// escapar do <c>[ApiController]</c> e chegar à validação de domínio.
/// </remarks>
public sealed record CriarUnidadeCommand(
    string? Nome,
    string? Alias,
    string? Slug,
    string? Sigla,
    string? Codigo,
    Guid? UnidadeSuperiorId,
    TipoUnidade Tipo,
    bool UnidadeAcademica,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? CidadeCodigoIbge = null,
    string? CidadeNome = null,
    string? CidadeUf = null) : ICommand<Result<Guid>>;
