namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação do pedido alterado com a captura do conflito de concorrência: com Idempotency-Key o handler captura o conflito
/// e descarta o rastreamento (ADR-0119), e o código de conflito sem a marca de gravação não prende a chave. O cancelamento
/// não passa por aqui: sem a chave, o conflito propaga e a API o responde como conflito genérico.
/// </summary>
internal static class GravacaoDoPedido
{
    /// <summary>Grava; no conflito, descarta o rastreamento e devolve a falha de conflito do pedido.</summary>
    public static async Task<Result> GravarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PedidoHandlerErros.ConflitoDeConcorrencia());
        }
    }
}
