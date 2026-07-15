namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarPrecedenciaFaseCommand"/>. Carrega a aresta (404
/// se inexistente) e atualiza o único atributo editável. Antecessora/sucessora
/// imutáveis: não há revalidação de self-loop/duplicata/ciclo (esses invariantes
/// dependem só do par, congelado desde a criação).
/// </summary>
public static class AtualizarPrecedenciaFaseCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarPrecedenciaFaseCommand command,
        IPrecedenciaFaseRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        PrecedenciaFase? aresta = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (aresta is null)
        {
            return Result.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.NaoEncontrada,
                "Aresta de precedência não encontrada."));
        }

        aresta.Atualizar(command.PermiteSobreposicao);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
