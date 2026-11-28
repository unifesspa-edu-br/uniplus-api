namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <remarks>
/// <c>Nome</c>/<c>Slug</c>/<c>Sigla</c>/<c>Codigo</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125) — mesma justificativa de <see cref="CriarUnidadeCommand"/>, que vale também para
/// <c>UnidadeAcademica</c>: a edição substitui o valor, e um <c>false</c> implícito o desligaria em silêncio.
/// </remarks>
public sealed record AtualizarUnidadeCommand(
    Guid Id,
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
    DateOnly? VigenciaFim,
    /// <summary>Motivo da mudança de identificador (Slug/Sigla/Codigo/Alias), se aplicável.</summary>
    string? MotivoMudancaIdentificador = null,
    string? CidadeCodigoIbge = null,
    string? CidadeNome = null,
    string? CidadeUf = null) : ICommand<Result>;
