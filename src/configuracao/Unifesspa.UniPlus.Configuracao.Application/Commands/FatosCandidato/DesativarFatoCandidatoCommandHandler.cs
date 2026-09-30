namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class DesativarFatoCandidatoCommandHandler
{
    public static Task<Result> Handle(
        DesativarFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return MutacaoDoFato.AplicarAsync(
            command.Id, static fato => fato.Desativar(), repository, unitOfWork, cancellationToken);
    }
}
