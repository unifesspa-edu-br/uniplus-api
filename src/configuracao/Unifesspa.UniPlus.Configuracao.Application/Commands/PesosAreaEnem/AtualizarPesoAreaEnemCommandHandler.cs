namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarPesoAreaEnemCommand"/>. Valida o payload (422,
/// sem I/O) ANTES de buscar a linha por Id — validação sempre vence 404. Edita
/// apenas o peso e o corte de cada área e a base legal — a chave de negócio (resolução + grupo) e o
/// <c>Id</c> são imutáveis — mudá-los caracterizaria outra linha, não uma edição
/// —, logo não há colisão de unicidade possível e nenhuma checagem de corrida é
/// necessária.
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

        IReadOnlyList<AreaInformada>? areas = AreasDoComando.ParaDominio(command.Areas);
        Result conferenciaDaDeclaracao = PesoAreaEnem.ValidarCamposDoPayload(areas, command.BaseLegal);

        return conferenciaDaDeclaracao.IsFailure
            ? Result.ValidationFailure(conferenciaDaDeclaracao.Errors)
            : await AtualizarAsync(command, areas, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarAsync(
        AtualizarPesoAreaEnemCommand command,
        IReadOnlyList<AreaInformada>? areas,
        IPesoAreaEnemRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        PesoAreaEnem? peso = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return peso is null
            ? Result.Failure(new DomainError(PesoAreaEnemErrorCodes.NaoEncontrado, "Linha de pesos do ENEM não encontrada."))
            : await AplicarAsync(peso, areas, command.BaseLegal, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(
        PesoAreaEnem peso,
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result atualizado = peso.Atualizar(areas, baseLegal);
        if (atualizado.IsFailure)
        {
            return atualizado;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
