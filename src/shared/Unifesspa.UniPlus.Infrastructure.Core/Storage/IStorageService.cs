namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(string bucket, string nomeArquivo, Stream conteudo, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);
    Task RemoverAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);
    Task<string> GerarUrlTemporariaAsync(string bucket, string nomeArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera uma URL pre-assinada de <c>PUT</c> para upload direto do cliente ao bucket,
    /// sem os bytes trafegarem pela API. <paramref name="contentType"/> entra na assinatura
    /// (header <c>Content-Type</c>) — o cliente precisa enviar exatamente esse valor no PUT,
    /// senão o MinIO recusa com <c>SignatureDoesNotMatch</c>. O SDK MinIO não expõe restrição
    /// de tamanho para PUT pre-assinado simples (isso só existe em <c>PresignedPostPolicyArgs</c>,
    /// upload via formulário); tamanho máximo é responsabilidade de validação server-side após
    /// o upload (ver <see cref="ObterMetadadosAsync"/>).
    /// </summary>
    Task<string> GerarUrlUploadTemporariaAsync(string bucket, string nomeArquivo, TimeSpan expiracao, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém tamanho e content-type do objeto sem baixar o conteúdo (HEAD/stat) — permite
    /// validar tamanho antes de um download potencialmente caro. Retorna <see langword="null"/>
    /// quando o objeto (ou o bucket) não existe, em vez de propagar exceção de vendor.
    /// </summary>
    Task<ObjetoMetadados?> ObterMetadadosAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baixa no máximo <paramref name="limiteBytes"/> do objeto via GET com Range (byte
    /// 0 a <paramref name="limiteBytes"/> - 1) — o MinIO nunca transmite mais que isso pela
    /// rede, então o limite é aplicado no servidor, não depois de já ter bufferizado o
    /// objeto inteiro em memória. Um objeto maior que o limite devolve exatamente
    /// <paramref name="limiteBytes"/> bytes (sem indicar o tamanho real); o caller decide o
    /// que fazer com isso (ex.: tratar como excedido).
    /// </summary>
    Task<Stream> DownloadLimitadoAsync(string bucket, string nomeArquivo, long limiteBytes, CancellationToken cancellationToken = default);
}

/// <summary>Metadados de um objeto armazenado, obtidos via stat/HEAD sem baixar o conteúdo.</summary>
public sealed record ObjetoMetadados(long TamanhoBytes, string ContentType);
