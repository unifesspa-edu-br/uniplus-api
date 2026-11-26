namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeCursoCommand"/>: a faixa do número e a conferência contra
/// as ofertas da distribuição são do agregado (<see cref="ProcessoSeletivo.DefinirOpcoesDeCurso"/>).
/// </summary>
public static class DefinirOpcoesDeCursoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirOpcoesDeCursoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
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
            return Result.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result result = processo.DefinirOpcoesDeCurso(command.Numero);
        if (result.IsFailure)
        {
            // Só a recusa de regra tem campo; o que vem sem campo é do agregado (precondição).
            return result.Errors[0].Field is null
                ? Result.Failure(result.Error!)
                : Result.ValidationFailure(result.Errors);
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
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O processo foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
