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

        Result<Inscricao> acesso = await AcessoAoDocumentoDaInscricao
            .ResolverInscricaoDoDonoAsync(query.ProcessoSeletivoId, userContext, inscricaoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(acesso.Error!);
        }

        Result<ArquivoDaApresentacao> encontrado = await AcessoAoDocumentoDaInscricao
            .ResolverArquivoAsync(query.ArquivoId, acesso.Value!, documentoRepository, cancellationToken)
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

        string nome = await NomeDeApresentacaoAsync(arquivo, documentoRepository, cancellationToken).ConfigureAwait(false);
        DateTimeOffset emitidoEm = clock.GetUtcNow();
        string url = await storage
            .GerarUrlLeituraAsync(arquivo.ChaveDefinitiva, nome, PrazosDoArquivoEnviado.Leitura, cancellationToken)
            .ConfigureAwait(false);

        LogAcessoEmitido(logger, userContext.UserId ?? string.Empty, arquivo.Id, emitidoEm);
        return Result<AcessoArquivoDoCandidatoDto>.Success(
            new AcessoArquivoDoCandidatoDto(new Uri(url, UriKind.Absolute), emitidoEm.Add(PrazosDoArquivoEnviado.Leitura)));
    }

    /// <summary>"Arquivo 1.pdf": a posição entre os arquivos da mesma apresentação e o formato; o nome original não é guardado.</summary>
    private static async Task<string> NomeDeApresentacaoAsync(
        ArquivoDaApresentacao arquivo, IDocumentoDaInscricaoRepository documentoRepository, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArquivoDaApresentacao> daInscricao = await documentoRepository
            .ListarArquivosDaInscricaoAsync(arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        int posicao = 1 + daInscricao.Count(a =>
            a.ApresentacaoId == arquivo.ApresentacaoId
            && a.ConfirmadoEm is { } confirmadoEm
            && (confirmadoEm < arquivo.ConfirmadoEm || (confirmadoEm == arquivo.ConfirmadoEm && a.Id.CompareTo(arquivo.Id) < 0)));
        return $"Arquivo {posicao}.{FormatosDoArquivoDoCandidato.Extensao(arquivo.Formato)}";
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Acesso de leitura ao arquivo {ArquivoId} do candidato {Usuario} emitido em {EmitidoEm}")]
    private static partial void LogAcessoEmitido(ILogger logger, string usuario, Guid arquivoId, DateTimeOffset emitidoEm);
}
