namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da desativação de motivo (UNI-REQ-0122).</summary>
public static class DesativarMotivoDeDecisaoCommandHandler
{
    public static Task<Result> Handle(
        DesativarMotivoDeDecisaoCommand command,
        IMotivoDeDecisaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return AlteracaoDoMotivo.AplicarAsync(command.Id, static motivo => motivo.Desativar(), repository, unitOfWork, cancellationToken);
    }
}
