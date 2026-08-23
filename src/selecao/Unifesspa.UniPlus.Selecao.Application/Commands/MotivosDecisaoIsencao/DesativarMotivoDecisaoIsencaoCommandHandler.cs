namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da desativação de motivo (UNI-REQ-0122).</summary>
public static class DesativarMotivoDecisaoIsencaoCommandHandler
{
    public static async Task<Result> Handle(
        DesativarMotivoDecisaoIsencaoCommand command,
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

        Result desativar = motivo.Desativar();
        if (desativar.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return desativar;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
