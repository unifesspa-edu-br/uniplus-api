namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public static class AceitarRecomendacaoDoTipoDocumentoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        AceitarRecomendacaoDoTipoDocumentoCommand command,
        ITipoDocumentoRepository tipos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AtorDoAto ator = AtorDoToken.De(userContext);
        DateTimeOffset agora = timeProvider.GetUtcNow();
        TipoDocumento? aceito = null;

        Result resultado = await MutacaoDoTipo.AplicarAsync(
            command.Id,
            tipo =>
            {
                Result aceita = tipo.AceitarRecomendacaoDaProtecao(ator.Sub, agora);
                if (aceita.IsSuccess)
                {
                    aceito = tipo;
                    atos.Registrar(tipo, AtoDaProtecao.Aceite, ator, agora);
                }

                return aceita;
            },
            tipos,
            unitOfWork,
            new DomainError(
                ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia,
                "O tipo de documento foi alterado concorrentemente; confira a proteção e tente de novo."),
            cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure || aceito is null)
        {
            return (resultado, []);
        }

        return (
            Result.Success(),
            AvisosDaProtecaoDoCatalogo.DoAceite(command.Destinatarios, "tipo de documento", aceito.Nome, aceito.Codigo.Valor, aceito.ClassificacaoProtecao));
    }
}
