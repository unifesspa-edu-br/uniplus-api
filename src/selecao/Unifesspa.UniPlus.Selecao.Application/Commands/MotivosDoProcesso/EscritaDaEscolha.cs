namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
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
    public static async Task<Result<MutacaoAceita>> GravarAsync(
        ProcessoSeletivo processo, ISelecaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex) || MotivoDeDecisaoUniqueConstraintViolation.EhEscolhaRepetida(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente,
                "Os motivos do processo foram alterados por outra operação. Consulte o estado atual antes de repetir."));
        }
    }

    public static Result<MutacaoAceita> ProcessoNaoEncontrado(Guid id) =>
        Result<MutacaoAceita>.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {id} não encontrado."));
}
