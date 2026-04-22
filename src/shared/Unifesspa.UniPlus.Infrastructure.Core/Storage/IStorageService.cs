namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(string bucket, string nomeArquivo, Stream conteudo, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);
    Task RemoverAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);
    Task<string> GerarUrlTemporariaAsync(string bucket, string nomeArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default);
}
