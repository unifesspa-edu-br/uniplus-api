namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

public sealed record CriarUnidadeCommand(
    string Nome,
    string? Alias,
    string Slug,
    string Sigla,
    string Codigo,
    Guid? UnidadeSuperiorId,
    TipoUnidade Tipo,
    bool UnidadeAcademica,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    OrigemUnidade Origem) : ICommand<Result<Guid>>;
