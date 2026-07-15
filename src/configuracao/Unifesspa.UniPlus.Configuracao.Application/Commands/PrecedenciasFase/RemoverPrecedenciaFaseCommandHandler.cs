namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverPrecedenciaFaseCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. Nunca bloqueado por referência: não há FK
/// intra-banco apontando para esta entidade — o único consumo é por leitura via
/// <c>IPrecedenciaFaseReader</c>.
/// </summary>
public static class RemoverPrecedenciaFaseCommandHandler
{
    public static async Task<Result> Handle(
        RemoverPrecedenciaFaseCommand command,
        IPrecedenciaFaseRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        PrecedenciaFase? aresta = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (aresta is null)
        {
            return Result.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.NaoEncontrada,
                "Aresta de precedência não encontrada."));
        }

        repository.Remover(aresta);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
