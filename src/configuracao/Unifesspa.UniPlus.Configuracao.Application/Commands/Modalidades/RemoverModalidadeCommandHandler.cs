namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverModalidadeCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia (409) as modalidades do catálogo legal fixo
/// (<c>RemocaoBloqueadaCodigoProtegido</c>) e bloqueia (409) quando ESTA modalidade é
/// referenciada por OUTRA modalidade viva como <c>composicao_origem</c> ou como
/// destino/par/fallback em <c>remanejamento_parametros</c> (integridade referencial
/// intra-banco, invariante 7). Nunca é bloqueada por snapshot-copy de Seleção
/// (ADR-0061). A auto-referência não bloqueia a própria remoção (checagem exclui o
/// próprio Id).
/// </summary>
public static class RemoverModalidadeCommandHandler
{
    public static async Task<Result> Handle(
        RemoverModalidadeCommand command,
        IModalidadeRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<Modalidade> resultado = await ObterRemovivelAsync(command.Id, repository, cancellationToken).ConfigureAwait(false);
        return resultado.IsFailure
            ? Result.ValidationFailure(resultado.Errors)
            : await RemoverSeNaoReferenciadaAsync(resultado.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // A proteção das modalidades legais é conferida antes das referências.
    private static async Task<Result<Modalidade>> ObterRemovivelAsync(
        Guid id, IModalidadeRepository repository, CancellationToken cancellationToken)
    {
        Modalidade? modalidade = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (modalidade is null)
        {
            return Result<Modalidade>.Failure(new DomainError(
                ModalidadeErrorCodes.NaoEncontrada,
                "Modalidade de concorrência não encontrada."));
        }

        return modalidade.Codigo.EhLegalFixa
            ? Result<Modalidade>.Failure(new DomainError(
                ModalidadeErrorCodes.RemocaoBloqueadaCodigoProtegido,
                "Esta modalidade pertence ao catálogo legal fixo e não pode ser removida."))
            : Result<Modalidade>.Success(modalidade);
    }

    // A consulta exclui o próprio identificador para permitir a autorreferência.
    private static async Task<Result> RemoverSeNaoReferenciadaAsync(
        Modalidade modalidade, IModalidadeRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository
            .EhReferenciadaPorOutraModalidadeVivaAsync(modalidade.Codigo.Valor, modalidade.Id, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.RemocaoBloqueadaPorReferencia,
                "Não é possível remover uma modalidade referenciada por outra modalidade viva "
                + "(como origem de composição, destino, par ou modalidade final do remanejamento)."));
        }

        repository.Remover(modalidade);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
