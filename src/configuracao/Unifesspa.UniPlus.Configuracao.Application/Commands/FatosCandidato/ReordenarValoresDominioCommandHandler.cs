namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ReordenarValoresDominioCommandHandler
{
    public static Task<Result> Handle(
        ReordenarValoresDominioCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return MutacaoDoFato.AplicarNosValoresAsync(
            command.FatoId, fato => fato.ReordenarValores(command.Codigos), repository, unitOfWork, cancellationToken);
    }
}
