namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A URL assinada de leitura de um arquivo verificado, a mesma resposta para o candidato que abre o próprio documento e
/// para quem opera o processo: só o arquivo verificado sai do armazenamento, com o nome que a listagem da inscrição mostra.
/// Quem chama já decidiu se a pessoa pode ler este arquivo.
/// </summary>
public static class EmissaoDoAcessoAoArquivo
{
    public static async Task<Result<AcessoArquivoDoCandidatoDto>> EmitirAsync(
        ArquivoDaApresentacao arquivo,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoDoCandidatoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        if (arquivo.Situacao != SituacaoDoArquivoDoCandidato.Verificado || arquivo.ChaveDefinitiva is null)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(ArquivoDaApresentacao.NaoVerificado);
        }

        string? nome = await NomeParaDownloadAsync(arquivo, documentoRepository, cancellationToken).ConfigureAwait(false);
        if (nome is null)
        {
            // O arquivo saiu da inscrição entre a busca e a releitura: para quem pede é o mesmo que não existir.
            return Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
        }

        DateTimeOffset emitidoEm = clock.GetUtcNow();
        string url = await storage
            .GerarUrlLeituraAsync(arquivo.ChaveDefinitiva, nome, PrazosDoArquivoEnviado.Leitura, cancellationToken)
            .ConfigureAwait(false);

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
}
