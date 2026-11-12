namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A gravação da decisão do item, comum à confirmação e à revogação.</summary>
internal static class GravacaoDoItem
{
    /// <summary>
    /// Grava a decisão que o agregado aceitou; na recusa, descarta o rastreamento. Com Idempotency-Key o conflito de
    /// concorrência se captura e descarta o rastreamento (ADR-0119).
    /// </summary>
    public static async Task<Result> GravarAsync(Result decisao, IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (decisao.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return decisao;
        }

        return await SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> SalvarAsync(IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(RevisaoHandlerErros.ConflitoDeConcorrencia());
        }
    }
}
