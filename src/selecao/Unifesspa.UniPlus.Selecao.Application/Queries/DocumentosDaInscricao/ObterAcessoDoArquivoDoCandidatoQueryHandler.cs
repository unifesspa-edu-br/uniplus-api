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
        if (arquivo.Situacao != SituacaoDoArquivoDoCandidato.Verificado || arquivo.ChaveDefinitiva is null)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(ArquivoDaApresentacao.NaoVerificado);
        }

        string? nome = await NomeParaDownloadAsync(arquivo, documentoRepository, cancellationToken).ConfigureAwait(false);
        if (nome is null)
        {
            // O arquivo saiu da inscrição entre a busca e a releitura: para o candidato é o mesmo que não existir.
            return Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
        }

        DateTimeOffset emitidoEm = clock.GetUtcNow();
        string url = await storage
            .GerarUrlLeituraAsync(arquivo.ChaveDefinitiva, nome, PrazosDoArquivoEnviado.Leitura, cancellationToken)
            .ConfigureAwait(false);

        LogAcessoEmitido(logger, userContext.UserId ?? string.Empty, arquivo.Id, emitidoEm);
        return Result<AcessoArquivoDoCandidatoDto>.Success(
            new AcessoArquivoDoCandidatoDto(new Uri(url, UriKind.Absolute), emitidoEm.Add(PrazosDoArquivoEnviado.Leitura)));
    }

    /// <summary>O nome que a listagem da inscrição informa para o arquivo: o download usa o mesmo nome que a tela mostra.</summary>
    private static async Task<string?> NomeParaDownloadAsync(
        ArquivoDaApresentacao arquivo, IDocumentoDaInscricaoRepository documentoRepository, CancellationToken cancellationToken)
    {
        ArquivosDaInscricao daInscricao = await ArquivosDaInscricao
            .CarregarAsync(documentoRepository, arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        return daInscricao.ParaOCandidato().SingleOrDefault(a => a.ArquivoId == arquivo.Id)?.NomeParaDownload;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Acesso de leitura ao arquivo {ArquivoId} do candidato {Usuario} emitido em {EmitidoEm}")]
    private static partial void LogAcessoEmitido(ILogger logger, string usuario, Guid arquivoId, DateTimeOffset emitidoEm);
}
