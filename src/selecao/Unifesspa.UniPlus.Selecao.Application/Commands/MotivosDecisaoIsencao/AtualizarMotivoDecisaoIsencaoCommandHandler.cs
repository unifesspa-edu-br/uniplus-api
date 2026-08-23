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

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
