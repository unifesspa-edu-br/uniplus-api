namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

public sealed record AtualizarUnidadeCommand(
    Guid Id,
    string Nome,
    string? Alias,
    string Slug,
    string Sigla,
    string Codigo,
    Guid? UnidadeSuperiorId,
    TipoUnidade Tipo,
    bool UnidadeAcademica,
    DateOnly? VigenciaFim,
    /// <summary>Motivo da mudança de identificador (Slug/Sigla/Codigo/Alias), se aplicável.</summary>
    string? MotivoMudancaIdentificador = null) : ICommand<Result>;
