namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O arquivo de outra pessoa recebe a recusa do inexistente. O GET aparece na auditoria do armazenamento com a
/// credencial da API, então cada emissão é registrada aqui (quem, qual arquivo, quando), sem a URL nem a chave.
/// </summary>
public static partial class ObterAcessoDoArquivoDoCandidatoQueryHandler
{
    public static async Task<Result<AcessoArquivoDoCandidatoDto>> Handle(
        ObterAcessoDoArquivoDoCandidatoQuery query,
        IInscricaoRepository inscricaoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoDoCandidatoStorage storage,
        IUserContext userContext,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        Result<ArquivoDaApresentacao> encontrado = await AcessoAoDocumentoDaInscricao
            .ResolverArquivoDoDonoAsync(
                query.ProcessoSeletivoId, query.ArquivoId, userContext, inscricaoRepository, documentoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (encontrado.IsFailure)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(encontrado.Error!);
        }

        ArquivoDaApresentacao arquivo = encontrado.Value!;
        Result<AcessoArquivoDoCandidatoDto> acesso = await EmissaoDoAcessoAoArquivo
            .EmitirAsync(arquivo, documentoRepository, storage, clock, cancellationToken)
            .ConfigureAwait(false);
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
