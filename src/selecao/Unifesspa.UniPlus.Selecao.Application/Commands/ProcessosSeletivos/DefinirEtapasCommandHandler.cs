namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirEtapasCommand"/>: trava o processo para edição, delega a definição das etapas,
/// que as reconcilia por <c>Id</c> (ver <see cref="DefinicaoDasEtapas"/>), com o relógio injetado lido <b>uma vez</b>
/// por operação (ADR-0068) e grava.
/// </summary>
/// <remarks>
/// SEM <c>[NonTransactional]</c>, de propósito: o repositório injetado direto no <c>Handle</c> é o que garante
/// a transação ambiente do Wolverine para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>, e o
/// <c>SELECT ... FOR UPDATE</c> só serializa handlers concorrentes enrolado nela. Os readers entram pelo
/// construtor de serviços públicos Scoped, que o codegen constrói inline, sem opt-in de service location novo.
/// </remarks>
public static class DefinirEtapasCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DefinicaoDasEtapas definicao,
        ISelecaoUnitOfWork unitOfWork,
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
    // defasado precisa saber disso antes de caçar um cadastro que não errou. Só a validação de schema do payload
    // a antecede, no middleware; o mesmo guard continua dentro do Definir* do domínio, que dá a garantia enquanto
    // esta antecipação dá a ordem.
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
        ISelecaoUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
