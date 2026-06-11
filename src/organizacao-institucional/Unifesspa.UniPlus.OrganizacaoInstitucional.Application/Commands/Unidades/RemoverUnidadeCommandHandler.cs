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
/// unidade viva, ou se for a raiz (reitoria) de uma Instituição viva.
/// </summary>
/// <remarks>
/// O bloqueio por <c>instituicao.unidade_raiz_id</c> preserva a integridade do
/// vínculo da Instituição com a reitoria (issue #585): uma Unidade referenciada
/// como raiz não pode ser removida enquanto a Instituição viva apontar para ela.
/// </remarks>
public static class RemoverUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverUnidadeCommand command,
        IUnidadeRepository repository,
        IInstituicaoRepository instituicaoRepository,
        IUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(instituicaoRepository);
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

        // NOTA: checagem check-then-act, simétrica à de PossuiSubordinadasVivasAsync
        // acima. Sob concorrência (remoção desta Unidade × criação/edição de uma
        // Instituição que a escolhe como raiz) a checagem pode ver `false` antes do
        // commit da Instituição, e como a remoção é soft-delete a FK Restrict não
        // barra o estado final (Instituição viva → Unidade removida). A serialização
        // estrita (lock/isolamento serializável) é controle de concorrência
        // cross-cutting comum a todos os cadastros — front separado, não desta Story.
        if (await instituicaoRepository.ExisteComUnidadeRaizAsync(command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao,
                "Não é possível remover uma Unidade que é raiz de uma Instituição."));
        }

        // SoftDeleteInterceptor converte EntityState.Deleted em soft-delete
        // (Unidade é ISoftDeletable), preenchendo DeletedBy/DeletedAt a partir de
        // IUserContext + TimeProvider. O Historico carregado por ObterPorIdAsync
        // NÃO é cascateado (FK Restrict, issue #629): a trilha append-only de
        // identificadores é preservada mesmo no soft-delete da Unidade.
        repository.Remover(unidade);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
