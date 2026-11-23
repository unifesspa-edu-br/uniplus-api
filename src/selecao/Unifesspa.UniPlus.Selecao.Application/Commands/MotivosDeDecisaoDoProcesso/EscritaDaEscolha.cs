namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisaoDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A gravação da escolha, comum à inclusão e à retirada: a rota exige chave de idempotência, então o
/// conflito de concorrência e a escolha repetida em corrida voltam como falha, com o rastreamento
/// descartado (ADR-0119).
/// </summary>
internal static class EscritaDaEscolha
{
    public static async Task<Result> GravarAsync(
        ProcessoSeletivo processo, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex) || MotivoDeDecisaoUniqueConstraintViolation.EhEscolhaRepetida(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente,
                "Os motivos do processo foram alterados por outra operação. Consulte o estado atual antes de repetir."));
        }
    }
}
