namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da edição de motivo. Só a descrição muda; o código
/// permanece o mesmo, e por isso não há unicidade a reconferir aqui.
/// </summary>
public static class AtualizarMotivoDecisaoIsencaoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarMotivoDecisaoIsencaoCommand command,
        IMotivoDecisaoIsencaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        MotivoDecisaoIsencao? motivo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (motivo is null)
        {
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.NaoEncontrado(command.Id));
        }

        Result atualizar = motivo.Atualizar(command.Descricao);
        if (atualizar.IsFailure)
        {
            // A entidade foi carregada rastreada e a tentativa de edição pode
            // ter chegado até aqui sem mutar nada; ainda assim o descarte é
            // necessário, porque o SaveChangesAsync do Wolverine roda depois do
            // handler mesmo quando ele devolve falha.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return atualizar;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // A edição grava a mesma linha que a ativação e a desativação, e o
            // xmin as faz colidir entre si. Deixar a exceção escapar não
            // devolveria apenas um erro de servidor: o filtro de idempotência
            // não apaga a reserva de uma requisição que terminou em exceção —
            // ele não tem como saber se a mutação chegou a ser aplicada —, e a
            // entrada ficaria em processamento até o TTL, respondendo conflito
            // de processamento a todo retry com a mesma chave. Traduzir aqui
            // completa a entrada normalmente e devolve o conflito real.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.SituacaoAlteradaConcorrentemente());
        }

        return Result.Success();
    }
}
