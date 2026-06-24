namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarPesoAreaEnemCommand"/>. Edita apenas pesos,
/// corte e base legal — a chave de negócio (resolução + grupo) e o <c>Id</c> são
/// imutáveis (CA-04b), logo não há colisão de unicidade possível e nenhuma
/// checagem de corrida é necessária.
/// </summary>
public static class AtualizarPesoAreaEnemCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarPesoAreaEnemCommand command,
        IPesoAreaEnemRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        PesoAreaEnem? peso = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (peso is null)
        {
            return Result.Failure(new DomainError(
                PesoAreaEnemErrorCodes.NaoEncontrado,
                "Linha de pesos do ENEM não encontrada."));
        }

        Result atualizarResult = peso.Atualizar(
            command.PesoRedacao,
            command.PesoCienciasNatureza,
            command.PesoCienciasHumanas,
            command.PesoLinguagens,
            command.PesoMatematica,
            command.CorteRedacao,
            command.BaseLegal);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
