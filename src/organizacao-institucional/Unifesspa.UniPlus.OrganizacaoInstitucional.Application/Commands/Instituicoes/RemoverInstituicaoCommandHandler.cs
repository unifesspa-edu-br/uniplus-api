namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="RemoverInstituicaoCommand"/>. Executa soft-delete via
/// <c>SoftDeleteInterceptor</c>; o registro removido permanece na trilha de
/// auditoria e deixa de contar para o limite singleton.
/// </summary>
public static class RemoverInstituicaoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverInstituicaoCommand command,
        IInstituicaoRepository repository,
        IOrganizacaoInstitucionalUnitOfWork unitOfWork,
        IInstituicaoCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Instituicao? instituicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (instituicao is null)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.NaoEncontrada,
                "Instituição não encontrada."));
        }

        // SoftDeleteInterceptor converte EntityState.Deleted em soft-delete
        // preenchendo DeletedBy/DeletedAt a partir de IUserContext + TimeProvider.
        repository.Remover(instituicao);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
