namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
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
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        IReadOnlyList<AreaInformada>? areas =
            AreasDoComando.ParaDominio(command.Areas);

        Result preCheck = PesoAreaEnem.ValidarCamposDoPayload(areas, command.BaseLegal);

        if (preCheck.IsFailure)
        {
            return Result.ValidationFailure(preCheck.Errors);
        }

        PesoAreaEnem? peso = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (peso is null)
        {
            return Result.Failure(new DomainError(
                PesoAreaEnemErrorCodes.NaoEncontrado,
                "Linha de pesos do ENEM não encontrada."));
        }

        Result atualizarResult = peso.Atualizar(areas, command.BaseLegal);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        // Os valores editados vivem nas linhas filhas de áreas; sem marcar a linha de
        // pesos, uma edição só de peso ou corte não carimbaria UpdatedAt/UpdatedBy.
        repository.RegistrarAtualizacao(peso);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
