namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AdicionarValorDominioCommandHandler
{
    public static Task<Result> Handle(
        AdicionarValorDominioCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        return MutacaoDoFato.AplicarAsync(
            command.FatoId, fato => fato.AdicionarValorDominio(command.Codigo, command.Descricao, command.Ordem, ativo: true, command.Orientacao), repository, unitOfWork, cancellationToken);
    }
}
