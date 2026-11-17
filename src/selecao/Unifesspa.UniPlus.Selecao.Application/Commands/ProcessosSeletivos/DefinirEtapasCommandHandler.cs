namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// Substitui as etapas do processo, reconciliando por <c>Id</c> (ver <see cref="DefinicaoDasEtapas"/>).
    /// </summary>
    /// <remarks>
    /// SEM <c>[NonTransactional]</c>, de propósito: este handler depende da transação ambiente do Wolverine
    /// para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>. O <c>SELECT ... FOR UPDATE</c>
    /// só serializa handlers concorrentes porque roda enrolado nela, e <c>[NonTransactional]</c> o
    /// desabilitaria.
    /// </remarks>
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result<MutacaoAceita>.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, definicao, unitOfWork, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    // A precondição precede as regras de negócio que a definição avalia, na ordem da ADR-0110: quem tem If-Match
    // defasado precisa saber disso antes de caçar um cadastro que não errou. Só a validação de schema do payload a
    // antecede, no middleware; o mesmo guard continua dentro do Definir* do domínio, que dá a garantia enquanto esta
    // antecipação dá a ordem.
    private static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, DefinirEtapasCommand command, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result<MutacaoAceita>> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
