namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverTipoBancaCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. Nunca bloqueado por referência: o único consumo do
/// tipo de banca é por snapshot-copy desacoplado no Módulo Seleção (ADR-0061), que
/// não impede a remoção lógica; e não há FK intra-banco apontando para ele.
/// </summary>
public static class RemoverTipoBancaCommandHandler
{
    public static async Task<Result> Handle(
        RemoverTipoBancaCommand command,
        ITipoBancaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoBanca? banca = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (banca is null)
        {
            return Result.Failure(new DomainError(
                TipoBancaErrorCodes.NaoEncontrado,
                "Tipo de banca não encontrado."));
        }

        repository.Remover(banca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
