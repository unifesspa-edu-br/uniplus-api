namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="RemoverUnidadeCommand"/>. Executa soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia se a unidade for superior de outra
/// unidade viva.
/// </summary>
/// <remarks>
/// O bloqueio por <c>instituicao.unidade_raiz_id</c> (CA-06 §"raiz da
/// Instituição") será adicionado quando o agregado Instituicao for implementado
/// (issue #585), que depende desta Story.
/// </remarks>
public static class RemoverUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverUnidadeCommand command,
        IUnidadeRepository repository,
        IUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Unidade? unidade = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (unidade is null)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.NaoEncontrada,
                "Unidade não encontrada."));
        }

        if (await repository.PossuiSubordinadasVivasAsync(command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas,
                "Não é possível remover uma Unidade que possui unidades subordinadas ativas."));
        }

        // SoftDeleteInterceptor converte EntityState.Deleted em soft-delete
        // preenchendo DeletedBy/DeletedAt a partir de IUserContext + TimeProvider.
        repository.Remover(unidade);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
