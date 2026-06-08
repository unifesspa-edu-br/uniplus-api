namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public static class AtualizarUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarUnidadeCommand command,
        IUnidadeRepository repository,
        IUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Unidade? unidade = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (unidade is null)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.NaoEncontrada,
                "Unidade não encontrada."));
        }

        Result<Slug> slugResult = Slug.From(command.Slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure(slugResult.Error!);
        }

        Slug slug = slugResult.Value!;

        if (!string.Equals(slug.Valor, unidade.Slug.Valor, StringComparison.OrdinalIgnoreCase)
            && await repository.SlugExisteEntreLivosAsync(slug, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SlugJaExiste,
                $"Já existe uma Unidade viva com o slug '{slug}'."));
        }

        if (!string.Equals(command.Sigla, unidade.Sigla, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(command.Sigla, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SiglaJaExiste,
                $"Já existe uma Unidade viva com a sigla '{command.Sigla}'."));
        }

        if (!string.Equals(command.Codigo, unidade.Codigo, StringComparison.OrdinalIgnoreCase)
            && await repository.CodigoExisteEntreLivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.CodigoJaExiste,
                $"Já existe uma Unidade viva com o código '{command.Codigo}'."));
        }

        if (command.UnidadeSuperiorId.HasValue)
        {
            if (command.UnidadeSuperiorId.Value == command.Id)
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorFormaCiclo,
                    "Uma Unidade não pode ser superior de si mesma."));
            }

            Unidade? superior = await repository.ObterPorIdAsync(
                command.UnidadeSuperiorId.Value, cancellationToken).ConfigureAwait(false);

            if (superior is null)
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorNaoEncontrado,
                    "A Unidade superior informada não foi encontrada."));
            }

            if (await repository.FormariaCicloAsync(command.Id, command.UnidadeSuperiorId.Value, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorFormaCiclo,
                    "A Unidade superior informada é descendente da Unidade sendo editada — formaria ciclo na hierarquia."));
            }
        }

        DateOnly dataAtual = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        Result atualizarResult = unidade.Atualizar(
            command.Nome,
            command.Alias,
            slug,
            command.Sigla,
            command.Codigo,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica,
            command.VigenciaFim,
            dataAtual,
            command.MotivoMudancaIdentificador);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
