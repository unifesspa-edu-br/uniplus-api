namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A lista de códigos ausente é recusada antes de buscar o fato: a validação vence a inexistência (ADR-0125).</summary>
public static class ReordenarValoresDominioCommandHandler
{
    public static Task<Result> Handle(
        ReordenarValoresDominioCommand command,
        IFatoCandidatoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result presenca = FatoCandidato.ConferirPresencaDaOrdemDosValores(command.Codigos);

        return presenca.IsFailure
            ? Task.FromResult(presenca)
            : MutacaoDoFato.AplicarAsync(
                command.FatoId, fato => fato.ReordenarValores(command.Codigos), repository, unitOfWork, cancellationToken);
    }
}
