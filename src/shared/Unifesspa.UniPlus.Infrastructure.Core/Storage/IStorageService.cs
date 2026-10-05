namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(string bucket, string nomeArquivo, Stream conteudo, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default);
    /// <summary>
    /// Remove o objeto. Objeto (ou bucket) inexistente não é erro: o estado pedido — o objeto não
    /// estar lá — já vale, e quem repete a remoção depois de uma falha parcial não pode travar nele.
    /// </summary>
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

    /// <summary>
    /// Copia, dentro do armazenamento, o objeto de origem para o destino como objeto <b>imutável de
    /// leitura pública</b>: grava nele o tipo do conteúdo, a política de cache perpétuo, o nome com
    /// que o arquivo se apresenta a quem o baixa e os metadados de procedência. Os bytes não passam
    /// pela aplicação.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nunca sobrescreve.</b> Destino existente é o resultado de uma cópia anterior — o endereço
    /// é derivado do conteúdo e da procedência —, e nada é gravado: devolve <see langword="false"/>.
    /// </para>
    /// <para>
    /// <b>Não cria o bucket de destino.</b> O bucket público nasce com a política de leitura anônima
    /// que a infraestrutura aplica; criado aqui, nasceria privado e os endereços divulgados
    /// responderiam 403 sem que nada acusasse. Bucket ausente é exceção.
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> quando copiou; <see langword="false"/> quando o destino já existia.</returns>
    Task<bool> CopiarComoObjetoPublicoAsync(
        string bucketOrigem,
        string chaveOrigem,
        string bucketDestino,
        string chaveDestino,
        ApresentacaoDoObjetoPublico apresentacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Metadados de um objeto armazenado, obtidos via stat/HEAD sem baixar o conteúdo.</summary>
public sealed record ObjetoMetadados(long TamanhoBytes, string ContentType);

/// <summary>
/// O que o objeto público precisa dizer a quem o baixa, gravado nele porque nenhuma aplicação está
/// no caminho da leitura.
/// </summary>
/// <param name="ContentType">Tipo do conteúdo.</param>
/// <param name="NomeDeApresentacao">Nome legível com que o arquivo é salvo; sem ele, o nome seria a chave.</param>
/// <param name="Procedencia">
/// Metadados de procedência, por nome sem prefixo (ex.: <c>sha256</c>). Valores em ASCII: são
/// identificadores, nunca dado pessoal.
/// </param>
public sealed record ApresentacaoDoObjetoPublico(
    string ContentType,
    string NomeDeApresentacao,
    IReadOnlyDictionary<string, string> Procedencia);
