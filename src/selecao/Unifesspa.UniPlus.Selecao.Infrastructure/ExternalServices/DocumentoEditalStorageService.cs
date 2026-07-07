namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Implementação de <see cref="IDocumentoEditalStorage"/> (Story #759, T3
/// #784) que envolve o <see cref="IStorageService"/> compartilhado de
/// <c>Infrastructure.Core</c>, resolvendo o bucket via
/// <see cref="StorageOptions.BucketName"/> — a única peça deste fluxo que
/// conhece o vendor MinIO/S3, mantendo o port em <c>Application.Abstractions</c>
/// livre de conceitos de infraestrutura.
/// </summary>
public sealed class DocumentoEditalStorageService : IDocumentoEditalStorage
{
    private const string ContentTypePdf = "application/pdf";

    private readonly IStorageService _storageService;
    private readonly string _bucket;

    public DocumentoEditalStorageService(IStorageService storageService, IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(options);

        _storageService = storageService;
        _bucket = options.Value.BucketName
            ?? throw new InvalidOperationException("Storage:BucketName não configurado — obrigatório para o upload de documentos do Edital.");
    }

    public Task<string> GerarUrlUploadAsync(string objectKey, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storageService.GerarUrlUploadTemporariaAsync(_bucket, objectKey, expiracao, ContentTypePdf, cancellationToken);

    public async Task<InfoObjetoArmazenado?> ObterInfoAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ObjetoMetadados? metadados = await _storageService
            .ObterMetadadosAsync(_bucket, objectKey, cancellationToken)
            .ConfigureAwait(false);

        return metadados is null ? null : new InfoObjetoArmazenado(metadados.TamanhoBytes, metadados.ContentType);
    }

    public Task<Stream> AbrirLeituraAsync(string objectKey, CancellationToken cancellationToken = default) =>
        _storageService.DownloadAsync(_bucket, objectKey, cancellationToken);
}
