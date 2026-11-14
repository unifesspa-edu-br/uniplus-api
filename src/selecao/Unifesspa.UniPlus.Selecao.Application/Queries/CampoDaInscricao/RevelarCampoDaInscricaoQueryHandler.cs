namespace Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A revelação de UM campo identificador de uma inscrição enviada: o CPF, o número do RG, do passaporte e qualquer outro
/// que o processo congelou como identificador (ADR-0153). Revelar é consulta de dado protegido a pedido, um registro por
/// vez, com a mesma decisão e o mesmo registro de qualquer outro acesso.
/// </summary>
/// <remarks>
/// A decisão é tomada aqui, e não na borda, para que a inscrição já esteja conferida quando o registro a nomear: a
/// trilha nunca cita inscrição que não existe. Depois do vínculo, e antes de procurar a resposta, exige a consulta da
/// inscrição na unidade dona do processo, a permissão da classe em que o identificador aparece (a que mostra o valor
/// mascarado) e a de revelar identificadores, nessa ordem (<see cref="AcessoARevelacaoDoCampo"/>). A inscrição que não
/// existe, a de outro processo, a em rascunho, o campo que não é identificador e a recusa por falta de permissão
/// respondem o mesmo 404. A resposta só é procurada com as três permissões. O valor só é decifrado depois da decisão
/// permitida, e o log traz quem revelou e qual campo, nunca o valor.
/// </remarks>
public static partial class RevelarCampoDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: a consulta não grava no banco, e o registro do acesso é publicado de forma síncrona no
    /// Kafka. A transação ambiente do Wolverine seguraria a conexão aberta até o broker confirmar.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<CampoReveladoDto>> Handle(
        RevelarCampoDaInscricaoQuery query,
        AcessoARevelacaoDoCampo acesso,
        LeituraDoIdentificador leitura,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<VinculoDaInscricao> permitido = await acesso
            .DecidirAsync(query.ProcessoSeletivoId, query.InscricaoId, query.FatoCodigo, query.Ocorrencia, cancellationToken)
            .ConfigureAwait(false);

        return permitido.IsFailure
            ? Result<CampoReveladoDto>.Failure(permitido.Error!)
            : await RevelarERegistrarAsync(query, permitido.Value!, leitura, userContext, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<CampoReveladoDto>> RevelarERegistrarAsync(
        RevelarCampoDaInscricaoQuery query,
        VinculoDaInscricao vinculo,
        LeituraDoIdentificador leitura,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Result<CampoReveladoDto> campo = await leitura
            .RevelarAsync(vinculo, query.FatoCodigo, query.Ocorrencia, cancellationToken)
            .ConfigureAwait(false);
        if (campo.IsSuccess)
        {
            LogCampoRevelado(logger, userContext.UserId ?? string.Empty, campo.Value!.FatoCodigo, query.ProcessoSeletivoId, query.InscricaoId);
        }

        return campo;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Campo {FatoCodigo} da inscrição {InscricaoId} do processo {ProcessoSeletivoId} revelado a {Usuario}")]
    private static partial void LogCampoRevelado(ILogger logger, string usuario, string fatoCodigo, Guid processoSeletivoId, Guid inscricaoId);
}
