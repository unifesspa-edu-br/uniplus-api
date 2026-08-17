namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverModalidadeCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c> — bloqueia (409) as modalidades do catálogo legal fixo
/// (<c>RemocaoBloqueadaCodigoProtegido</c>) e bloqueia (409) quando ESTA modalidade é
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

        // As dez modalidades legais fixas são o piso de ações afirmativas exigido pela Lei
        // 12.711/2012 (red. Lei 14.723/2023): sem elas não há como configurar a distribuição
        // de vagas de um edital que aplique a lei. A checagem precede a de referência —
        // é determinística e não vai ao banco.
        if (modalidade.Codigo.EhLegalFixa)
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.RemocaoBloqueadaCodigoProtegido,
                "Esta modalidade pertence ao catálogo legal fixo e não pode ser removida."));
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
