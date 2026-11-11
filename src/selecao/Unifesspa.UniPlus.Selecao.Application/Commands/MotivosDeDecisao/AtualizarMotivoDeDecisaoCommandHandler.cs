namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da edição de motivo; só a descrição muda, e por isso não há unicidade a reconferir.</summary>
public static class AtualizarMotivoDeDecisaoCommandHandler
{
    public static Task<Result> Handle(
        AtualizarMotivoDeDecisaoCommand command,
        IMotivoDeDecisaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return AlteracaoDoMotivo.AplicarAsync(command.Id, motivo => motivo.Atualizar(command.Descricao), repository, unitOfWork, cancellationToken);
    }
}
