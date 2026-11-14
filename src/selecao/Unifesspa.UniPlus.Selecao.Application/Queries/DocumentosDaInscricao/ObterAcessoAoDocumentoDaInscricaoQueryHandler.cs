namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using Wolverine.Attributes;

/// <summary>
/// O acesso de quem opera o processo ao documento de uma inscrição. A sensibilidade do recurso é a classificação
/// congelada da exigência a que o arquivo responde (ADR-0136): cada classificação exige a sua própria permissão de
/// documentos, e a que não se resolve vale como sensível. Consultar as inscrições é condição para abrir documento, mas
/// não o abre: sem a consulta da inscrição na unidade dona, a permissão da classificação não vale.
/// </summary>
/// <remarks>
/// <para>
/// A ordem (<see cref="DecisaoDoAcessoAoDocumento"/>): o processo e a unidade dona; o arquivo e o vínculo da inscrição,
/// por leituras que não decifram CPF nem respostas; a consulta da inscrição; e então UMA decisão, com a permissão que a
/// classificação exige e a sensibilidade real do recurso. O que não existe, é de outra inscrição ou processo, ou é de
/// inscrição em rascunho recebe a recusa sem decisão gravada, pois não há recurso sobre o qual decidir; assim a trilha
/// nunca registra uma negativa de permissão que não foi a exigida.
/// </para>
/// <para>
/// A recusa cobre numa mesma resposta o que não existe e o que a pessoa não pode ler: nenhum revela o outro. A decisão
/// fica no registro de decisões de acesso; o log traz só quem abriu e qual arquivo, sem a URL nem a chave.
/// </para>
/// </remarks>
public static partial class ObterAcessoAoDocumentoDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: a consulta não grava no banco, e o registro do acesso é publicado de forma síncrona no
    /// Kafka. A transação ambiente do Wolverine seguraria a conexão aberta até o broker confirmar.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<AcessoArquivoDoCandidatoDto>> Handle(
        ObterAcessoAoDocumentoDaInscricaoQuery query,
        DecisaoDoAcessoAoDocumento acesso,
        EmissaoDoAcessoAoArquivo emissao,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(emissao);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<ArquivoDaApresentacao> permitido = await acesso
            .DecidirAsync(query.ProcessoSeletivoId, query.InscricaoId, query.ArquivoId, cancellationToken)
            .ConfigureAwait(false);

        return permitido.IsFailure
            ? Result<AcessoArquivoDoCandidatoDto>.Failure(permitido.Error!)
            : await EmitirERegistrarAsync(permitido.Value!, emissao, userContext, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AcessoArquivoDoCandidatoDto>> EmitirERegistrarAsync(
        ArquivoDaApresentacao arquivo, EmissaoDoAcessoAoArquivo emissao, IUserContext userContext, ILogger logger, CancellationToken cancellationToken)
    {
        Result<AcessoArquivoDoCandidatoDto> emitido = await emissao.EmitirAsync(arquivo, cancellationToken).ConfigureAwait(false);
        if (emitido.IsSuccess)
        {
            LogAcessoEmitido(
                logger, userContext.UserId ?? string.Empty, arquivo.Id, arquivo.InscricaoId,
                emitido.Value!.ExpiraEm - PrazosDoArquivoEnviado.Leitura);
        }

        return emitido;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Acesso de leitura ao arquivo {ArquivoId} da inscrição {InscricaoId} emitido a {Usuario} em {EmitidoEm}")]
    private static partial void LogAcessoEmitido(ILogger logger, string usuario, Guid arquivoId, Guid inscricaoId, DateTimeOffset emitidoEm);
}
