namespace Unifesspa.UniPlus.Verificador.Configuracao;

public sealed class VerificadorOptions
{
    public const string SectionName = "Verificador";

    public long TamanhoMaximoBytes { get; set; } = 20L * 1024 * 1024;

    public int Paralelismo { get; set; } = 4;

    /// <summary>Onde o arquivo em verificação é gravado; vazio usa o diretório temporário do processo.</summary>
    public string DiretorioTemporario { get; set; } = string.Empty;

    public ClamdOptions Clamd { get; set; } = new();

    public ArmazenamentoOptions Armazenamento { get; set; } = new();

    public RabbitMqOptions RabbitMq { get; set; } = new();
}

/// <summary>
/// <see cref="StreamMaxLengthBytes"/> e <see cref="MaxThreads"/> repetem o <c>clamd.conf</c> da imagem,
/// porque o clamd não os informa; o verificador os confere na subida.
/// </summary>
public sealed class ClamdOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 3310;

    public int TimeoutSegundos { get; set; } = 60;

    public long StreamMaxLengthBytes { get; set; } = 25L * 1024 * 1024;

    public int MaxThreads { get; set; } = 5;
}

public sealed class ArmazenamentoOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool UsarSsl { get; set; }

    public string BucketEnvios { get; set; } = string.Empty;

    public string BucketArquivos { get; set; } = string.Empty;
}

public sealed class RabbitMqOptions
{
    /// <summary>A URI completa (<c>amqp://usuario:senha@host:5672/vhost</c>); quando presente, vale no lugar dos campos abaixo.</summary>
    public string Uri { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string Usuario { get; set; } = string.Empty;

    public string Senha { get; set; } = string.Empty;
}
