namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da reativação de motivo.</summary>
public static class ReativarMotivoDeDecisaoCommandHandler
{
    public static Task<Result> Handle(
        ReativarMotivoDeDecisaoCommand command,
        IMotivoDeDecisaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return AlteracaoDoMotivo.AplicarAsync(command.Id, static motivo => motivo.Reativar(), repository, unitOfWork, cancellationToken);
    }
}
