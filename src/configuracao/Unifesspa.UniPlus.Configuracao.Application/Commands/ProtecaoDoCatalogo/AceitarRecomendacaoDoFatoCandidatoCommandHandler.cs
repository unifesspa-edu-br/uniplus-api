namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public static class AceitarRecomendacaoDoFatoCandidatoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        AceitarRecomendacaoDoFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IPrecedenciaFaseRepository precedenciaRepository,
        IModeloFormularioRepository modeloRepository,
        IModalidadeRepository modalidadeRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(modeloRepository);
        ArgumentNullException.ThrowIfNull(modalidadeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AtorDoAto ator = AtorDoToken.De(userContext);
        DateTimeOffset agora = timeProvider.GetUtcNow();
        (string Nome, string Codigo, ClassificacaoDeProtecao Classificacao)? aceito = null;

        Result resultado = await AlteracaoConferidaDoFato.AplicarAsync(
            command.Id,
            (fato, catalogo) =>
            {
                Result aceita = fato.AceitarRecomendacaoDaProtecao(ator.Sub, agora, catalogo);
                if (aceita.IsSuccess)
                {
                    aceito = (fato.Nome, fato.Codigo, fato.ClassificacaoProtecao);
                    atos.Registrar(fato, AtoDaProtecao.Aceite, ator, agora);
                }

                return aceita;
            },
            repository,
            precedenciaRepository,
            modeloRepository,
            modalidadeRepository,
            unitOfWork,
            cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure || aceito is not { } fatoAceito)
        {
            return (resultado, []);
        }

        return (
            Result.Success(),
            AvisosDaProtecaoDoCatalogo.DoAceite(command.Destinatarios, "fato", fatoAceito.Nome, fatoAceito.Codigo, fatoAceito.Classificacao));
    }
}
