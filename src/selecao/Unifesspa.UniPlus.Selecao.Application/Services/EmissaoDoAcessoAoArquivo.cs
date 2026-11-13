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
public sealed class EmissaoDoAcessoAoArquivo
{
    private readonly IDocumentoDaInscricaoRepository _documentos;
    private readonly IArquivoDoCandidatoStorage _storage;
    private readonly TimeProvider _clock;

    public EmissaoDoAcessoAoArquivo(IDocumentoDaInscricaoRepository documentos, IArquivoDoCandidatoStorage storage, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        _documentos = documentos;
        _storage = storage;
        _clock = clock;
    }

    public async Task<Result<AcessoArquivoDoCandidatoDto>> EmitirAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        if (arquivo.Situacao != SituacaoDoArquivoDoCandidato.Verificado || arquivo.ChaveDefinitiva is null)
        {
            return Result<AcessoArquivoDoCandidatoDto>.Failure(ArquivoDaApresentacao.NaoVerificado);
        }

        string? nome = await NomeParaDownloadAsync(arquivo, cancellationToken).ConfigureAwait(false);

        // O arquivo saiu da inscrição entre a busca e a releitura: para quem pede é o mesmo que não existir.
        return nome is null
            ? Result<AcessoArquivoDoCandidatoDto>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado)
            : await GerarAcessoAsync(arquivo.ChaveDefinitiva, nome, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>O nome que a listagem da inscrição informa para o arquivo: o download usa o mesmo nome que a tela mostra.</summary>
    private async Task<string?> NomeParaDownloadAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken)
    {
        ArquivosDaInscricao daInscricao = await ArquivosDaInscricao
            .CarregarAsync(_documentos, arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        return daInscricao.ParaOCandidato().SingleOrDefault(a => a.ArquivoId == arquivo.Id)?.NomeParaDownload;
    }

    private async Task<Result<AcessoArquivoDoCandidatoDto>> GerarAcessoAsync(string chave, string nome, CancellationToken cancellationToken)
    {
        DateTimeOffset emitidoEm = _clock.GetUtcNow();
        string url = await _storage.GerarUrlLeituraAsync(chave, nome, PrazosDoArquivoEnviado.Leitura, cancellationToken).ConfigureAwait(false);

        return Result<AcessoArquivoDoCandidatoDto>.Success(
            new AcessoArquivoDoCandidatoDto(new Uri(url, UriKind.Absolute), emitidoEm.Add(PrazosDoArquivoEnviado.Leitura)));
    }
}
