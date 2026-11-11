namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O percurso comum da edição, da desativação e da reativação: carrega o motivo, aplica a
/// operação do agregado e grava, traduzindo o conflito de concorrência otimista (ADR-0119).
/// </summary>
internal static class AlteracaoDoMotivo
{
    public static async Task<Result> AplicarAsync(
        Guid id,
        Func<MotivoDeDecisao, Result> operacao,
        IMotivoDeDecisaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        MotivoDeDecisao? motivo = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (motivo is null)
        {
            return Result.Failure(MotivoDeDecisaoHandlerErros.NaoEncontrado(id));
        }

        Result alteracao = operacao(motivo);
        if (alteracao.IsFailure)
        {
            // O SaveChangesAsync do Wolverine roda depois do handler mesmo quando ele devolve falha.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return alteracao;
        }

        return await GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarAsync(ISelecaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // As rotas exigem chave de idempotência: deixar a exceção escapar prenderia a reserva da
            // chave até o TTL. Descartar evita que o SaveChanges seguinte repita o mesmo UPDATE.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(MotivoDeDecisaoHandlerErros.SituacaoAlteradaConcorrentemente());
        }
    }
}
