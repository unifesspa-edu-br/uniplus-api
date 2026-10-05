namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Implementação de <see cref="IArquivoArmazenadoStorage"/> que envolve o <see cref="IStorageService"/> compartilhado de
/// <c>Infrastructure.Core</c>, resolvendo o bucket via
/// <see cref="StorageOptions.BucketName"/> — a única peça deste fluxo que
/// conhece o vendor MinIO/S3, mantendo o port em <c>Application.Abstractions</c>
/// livre de conceitos de infraestrutura.
/// </summary>
public sealed class ArquivoArmazenadoStorageService : IArquivoArmazenadoStorage
{
    private readonly IStorageService _storageService;
    private readonly string _bucket;

    public ArquivoArmazenadoStorageService(IStorageService storageService, IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(options);

        _storageService = storageService;
        _bucket = BucketDosArquivosEnviados.De(options.Value);
    }

    public Task<string> GerarUrlUploadAsync(string objectKey, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storageService.GerarUrlUploadTemporariaAsync(_bucket, objectKey, expiracao, contentType, cancellationToken);

    public Task<string> GerarUrlLeituraAsync(string objectKey, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storageService.GerarUrlTemporariaAsync(_bucket, objectKey, expiracao, cancellationToken);

    public async Task<InfoObjetoArmazenado?> ObterInfoAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ObjetoMetadados? metadados = await _storageService
            .ObterMetadadosAsync(_bucket, objectKey, cancellationToken)
            .ConfigureAwait(false);

        return metadados is null ? null : new InfoObjetoArmazenado(metadados.TamanhoBytes, metadados.ContentType);
    }

    public Task<Stream> AbrirLeituraAsync(string objectKey, long limiteBytes, CancellationToken cancellationToken = default) =>
        _storageService.DownloadLimitadoAsync(_bucket, objectKey, limiteBytes, cancellationToken);

    public async Task SalvarConteudoSeladoAsync(string objectKey, byte[] conteudo, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        using MemoryStream stream = new(conteudo);
        await _storageService.UploadAsync(_bucket, objectKey, stream, contentType, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoverAsync(string objectKey, CancellationToken cancellationToken = default) =>
        _storageService.RemoverAsync(_bucket, objectKey, cancellationToken);
}
