namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeCursoCommand"/>: a faixa do número e a conferência contra
/// as ofertas da distribuição são do agregado (<see cref="ProcessoSeletivo.DefinirOpcoesDeCurso"/>).
/// </summary>
public static class DefinirOpcoesDeCursoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirOpcoesDeCursoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida antes da regra de negócio (ADR-0110), e a recusa dela
        // (412/428) não se embrulha como erro de campo.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        Result result = processo.DefinirOpcoesDeCurso(command.Numero, command.Precondicao);
        if (result.IsFailure)
        {
            // Só a recusa de regra tem campo; o que vem sem campo é do agregado (precondição).
            return result.Errors[0].Field is null
                ? Result<MutacaoAceita>.Failure(result.Error!)
                : Result<MutacaoAceita>.ValidationFailure(result.Errors);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // O endpoint tem Idempotency-Key (ADR-0119): captura local e descarte, para o
            // SaveChangesAsync do outbox não reencontrar o agregado modificado.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O processo foi alterado concorrentemente. Tente novamente."));
        }

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
