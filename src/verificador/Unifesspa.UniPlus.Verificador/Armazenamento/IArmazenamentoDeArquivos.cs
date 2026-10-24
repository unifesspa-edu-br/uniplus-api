namespace Unifesspa.UniPlus.Verificador.Armazenamento;

/// <summary>
/// Um objeto do armazenamento. <see cref="Sha256"/> e <see cref="ETagDoEnvio"/> só existem no
/// objeto definitivo, gravados no metadado pela promoção.
/// </summary>
public sealed record ObjetoArmazenado(
    string ETag,
    long TamanhoBytes,
    string? Sha256,
    string? ETagDoEnvio);

/// <summary>Os buckets de envio (quarentena) e definitivo, do ponto de vista do verificador.</summary>
public interface IArmazenamentoDeArquivos
{
    /// <summary>O objeto no bucket definitivo, ou <see langword="null"/> se a chave não foi promovida.</summary>
    Task<ObjetoArmazenado?> ConsultarDefinitivoAsync(string chave, CancellationToken cancellationToken);

    /// <summary>O objeto no bucket de envio, ou <see langword="null"/> se a chave não existe.</summary>
    Task<ObjetoArmazenado?> ConsultarEnvioAsync(string chave, CancellationToken cancellationToken);

    /// <summary>
    /// Copia para <paramref name="destino"/> a versão do envio com este ETag, já decifrada pelo
    /// armazenamento. Devolve <see langword="false"/> se essa versão não existe mais.
    /// </summary>
    Task<bool> LerEnvioAsync(string chave, string etag, Stream destino, CancellationToken cancellationToken);

    /// <summary>Promove a versão do envio com este ETag para a chave definitiva, sem sobrescrever o que existir.</summary>
    Task PromoverAsync(
        string chaveDeEnvio,
        string etag,
        string chaveDefinitiva,
        string contentType,
        string sha256,
        CancellationToken cancellationToken);

    /// <summary>Remove a versão do envio com este ETag; uma versão diferente na chave não é tocada.</summary>
    Task RemoverEnvioAsync(string chave, string etag, CancellationToken cancellationToken);
}

/// <summary>Falha de leitura ou escrita no armazenamento (403, chave revogada no KES, rede).</summary>
public sealed class ArmazenamentoException : Exception
{
    public ArmazenamentoException()
    {
    }

    public ArmazenamentoException(string message)
        : base(message)
    {
    }

    public ArmazenamentoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
