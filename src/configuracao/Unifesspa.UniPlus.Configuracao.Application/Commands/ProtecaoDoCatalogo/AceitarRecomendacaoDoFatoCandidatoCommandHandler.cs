namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AceitarRecomendacaoDoFatoCandidatoCommandHandler
{
    public static Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        AceitarRecomendacaoDoFatoCandidatoCommand command,
        AlteracaoConferidaDoFato alteracoes,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new AceiteDaRecomendacao(atos, AtorDoToken.De(userContext), timeProvider.GetUtcNow())
            .AplicarAsync(command, alteracoes, cancellationToken);
    }
}
