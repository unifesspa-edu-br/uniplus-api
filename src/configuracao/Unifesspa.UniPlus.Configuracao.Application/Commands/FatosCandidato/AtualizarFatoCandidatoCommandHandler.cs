namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarFatoCandidatoCommandHandler
{
    public static Task<Result> Handle(
        AtualizarFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return MutacaoDoFato.AplicarAsync(
            command.Id, fato => fato.AlterarDescritivo(command.Nome, command.Descricao, TokensDoFato.Componente(command.ComponenteExibicao)), repository, unitOfWork, cancellationToken);
    }
}
