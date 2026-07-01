namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverFaseCanonicaCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. Nunca bloqueado por referência: o único consumo
/// da fase é por snapshot-copy desacoplado no Módulo Seleção (ADR-0061), que não
/// impede a remoção lógica; e não há FK intra-banco apontando para a fase.
/// </summary>
public static class RemoverFaseCanonicaCommandHandler
{
    public static async Task<Result> Handle(
        RemoverFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        FaseCanonica? fase = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (fase is null)
        {
            return Result.Failure(new DomainError(
                FaseCanonicaErrorCodes.NaoEncontrada,
                "Fase canônica não encontrada."));
        }

        repository.Remover(fase);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
