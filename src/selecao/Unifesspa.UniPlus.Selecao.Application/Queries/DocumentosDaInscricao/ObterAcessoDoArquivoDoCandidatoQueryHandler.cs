namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O arquivo de outra pessoa recebe a recusa do inexistente. O GET aparece na auditoria do armazenamento com a
/// credencial da API, então cada emissão é registrada aqui (quem, qual arquivo, quando), sem a URL nem a chave.
/// </summary>
public static partial class ObterAcessoDoArquivoDoCandidatoQueryHandler
{
    public static async Task<Result<AcessoArquivoDoCandidatoDto>> Handle(
        ObterAcessoDoArquivoDoCandidatoQuery query,
        AcessoAoDocumentoDaInscricao acesso,
        EmissaoDoAcessoAoArquivo emissao,
        IUserContext userContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(emissao);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<ArquivoDaApresentacao> encontrado = await acesso
            .ResolverArquivoDoDonoAsync(
                query.ProcessoSeletivoId, query.ArquivoId, cancellationToken)
            .ConfigureAwait(false);

        return encontrado.IsFailure
            ? Result<AcessoArquivoDoCandidatoDto>.Failure(encontrado.Error!)
            : await EmitirERegistrarAsync(encontrado.Value!, emissao, userContext, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AcessoArquivoDoCandidatoDto>> EmitirERegistrarAsync(
        ArquivoDaApresentacao arquivo, EmissaoDoAcessoAoArquivo emissao, IUserContext userContext, ILogger logger, CancellationToken cancellationToken)
    {
        Result<AcessoArquivoDoCandidatoDto> acesso = await emissao.EmitirAsync(arquivo, cancellationToken).ConfigureAwait(false);
        if (acesso.IsSuccess)
        {
            LogAcessoEmitido(logger, userContext.UserId ?? string.Empty, arquivo.Id, acesso.Value!.ExpiraEm - PrazosDoArquivoEnviado.Leitura);
        }

        return acesso;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Acesso de leitura ao arquivo {ArquivoId} do candidato {Usuario} emitido em {EmitidoEm}")]
    private static partial void LogAcessoEmitido(ILogger logger, string usuario, Guid arquivoId, DateTimeOffset emitidoEm);
}
