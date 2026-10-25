namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// <see cref="IArquivoDoCandidatoStorage"/> sobre o armazenamento com a credencial do candidato.
/// </summary>
public sealed class ArquivoDoCandidatoStorageService : IArquivoDoCandidatoStorage
{
    private readonly IStorageService _storageService;
    private readonly ArquivosDoCandidatoOptions _opcoes;

    public ArquivoDoCandidatoStorageService(
        ArmazenamentoDoCandidato armazenamento, IOptions<ArquivosDoCandidatoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(opcoes);

        _storageService = armazenamento.Storage;
        _opcoes = opcoes.Value;
    }

    public Task<string> GerarUrlUploadAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storageService.GerarUrlUploadTemporariaAsync(_opcoes.BucketDeEnvio, chaveDeEnvio, expiracao, contentType, cancellationToken);

    public Task<string> GerarUrlLeituraAsync(string chaveDefinitiva, string nomeDeApresentacao, TimeSpan expiracao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeDeApresentacao);

        // filename* (RFC 5987): o nome percent-encoded não injeta parâmetros no cabeçalho.
        string disposicao = $"inline; filename*=UTF-8''{Uri.EscapeDataString(nomeDeApresentacao)}";
        return _storageService.GerarUrlTemporariaComDisposicaoAsync(_opcoes.BucketDefinitivo, chaveDefinitiva, expiracao, disposicao, cancellationToken);
    }

    public Task RemoverDefinitivoAsync(string chaveDefinitiva, CancellationToken cancellationToken = default) =>
        _storageService.RemoverAsync(_opcoes.BucketDefinitivo, chaveDefinitiva, cancellationToken);
}
