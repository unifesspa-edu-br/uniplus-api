namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarPrecedenciaFaseCommand"/>. Recusa o campo ausente (422, que vence
/// a inexistência: ADR-0125), carrega a aresta (404 se inexistente) e atualiza o único atributo editável. Antecessora/sucessora
/// imutáveis: não há revalidação de self-loop/duplicata/ciclo (esses invariantes
/// dependem só do par, congelado desde a criação).
/// </summary>
public static class AtualizarPrecedenciaFaseCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarPrecedenciaFaseCommand command,
        IPrecedenciaFaseRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result presenca = PrecedenciaFase.ConferirSobreposicao(command.PermiteSobreposicao);

        return presenca.IsFailure
            ? presenca
            : await BuscarEAtualizarAsync(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> BuscarEAtualizarAsync(
        AtualizarPrecedenciaFaseCommand command, IPrecedenciaFaseRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        PrecedenciaFase? aresta = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return aresta is null
            ? Result.Failure(new DomainError(PrecedenciaFaseErrorCodes.NaoEncontrada, "Aresta de precedência não encontrada."))
            : await AtualizarEGravarAsync(aresta, command.PermiteSobreposicao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarEGravarAsync(
        PrecedenciaFase aresta, bool? permiteSobreposicao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result atualizada = aresta.Atualizar(permiteSobreposicao);
        if (atualizada.IsFailure)
        {
            return atualizada;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
