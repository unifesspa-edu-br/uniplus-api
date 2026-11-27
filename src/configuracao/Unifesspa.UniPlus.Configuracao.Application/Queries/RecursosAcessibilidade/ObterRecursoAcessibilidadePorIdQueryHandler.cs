namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterRecursoAcessibilidadePorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        RecursoAcessibilidadeErrorCodes.NaoEncontrado,
        "Recurso de acessibilidade não encontrado.");

    public static async Task<Result<RecursoAcessibilidadeDto>> Handle(
        ObterRecursoAcessibilidadePorIdQuery query,
        IRecursoAcessibilidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        RecursoAcessibilidade? recurso = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return recurso is null
            ? Result<RecursoAcessibilidadeDto>.Failure(NaoEncontrado)
            : Result<RecursoAcessibilidadeDto>.Success(recurso.ToDto());
    }
}
