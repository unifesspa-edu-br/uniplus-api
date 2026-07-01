namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverModalidadeCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia (409) quando ESTA modalidade é
/// referenciada por OUTRA modalidade viva como <c>composicao_origem</c> ou como
/// destino/par/fallback em <c>remanejamento_args</c> (integridade referencial
/// intra-banco, invariante 7). Nunca é bloqueada por snapshot-copy de Seleção
/// (ADR-0061). A auto-referência não bloqueia a própria remoção (checagem exclui o
/// próprio Id).
/// </summary>
public static class RemoverModalidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverModalidadeCommand command,
        IModalidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Modalidade? modalidade = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (modalidade is null)
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.NaoEncontrada,
                "Modalidade de concorrência não encontrada."));
        }

        // Check-then-act, simétrico ao bloqueio de remoção dos demais cadastros. A
        // serialização estrita sob concorrência é controle cross-cutting, fora desta
        // Story. O próprio Id é excluído — auto-referência não bloqueia a remoção.
        if (await repository
            .EhReferenciadaPorOutraModalidadeVivaAsync(modalidade.Codigo.Valor, modalidade.Id, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.RemocaoBloqueadaPorReferencia,
                "Não é possível remover uma modalidade referenciada por outra modalidade viva "
                + "(como origem de composição ou destino/par/fallback de remanejamento)."));
        }

        repository.Remover(modalidade);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
