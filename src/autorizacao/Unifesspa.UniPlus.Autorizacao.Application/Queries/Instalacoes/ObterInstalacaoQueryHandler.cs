namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura da instalação: a de outra pessoa não se distingue da inexistente.</summary>
public static class ObterInstalacaoQueryHandler
{
    public static async Task<Result<InstalacaoDto>> Handle(
        ObterInstalacaoQuery query,
        IInstalacaoRepository repository,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Result<InstalacaoDto>.Failure(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        Instalacao? instalacao = await repository
            .ObterDoExecutorAsync(query.Id, executor, cancellationToken)
            .ConfigureAwait(false);

        return instalacao is null
            ? Result<InstalacaoDto>.Failure(InstalacaoHandlerErros.NaoEncontrada())
            : Result<InstalacaoDto>.Success(InstalacaoMapping.ToDto(instalacao));
    }
}
