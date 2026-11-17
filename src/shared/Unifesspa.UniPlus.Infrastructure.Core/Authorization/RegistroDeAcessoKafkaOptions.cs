namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Kafka do registro de acesso (seção <c>RegistroDeAcesso:Kafka</c>). É independente do transporte do Wolverine
/// (<c>Kafka:</c>): ligar o registro não liga o transporte de eventos nem exige o Schema Registry, e a credencial é
/// um usuário próprio, só com escrita no tópico.
/// </summary>
public sealed class RegistroDeAcessoKafkaOptions
{
    /// <summary>Seção de configuração correspondente.</summary>
    public const string SectionName = "RegistroDeAcesso:Kafka";

    /// <summary>Tópico padrão dos registros de acesso (snake_case, ADR-0014).</summary>
    public const string TopicoPadrao = "autorizacao_registros_de_acesso";

    /// <summary>Lista de bootstrap servers (<c>host1:porta,host2:porta</c>). Obrigatório.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Tópico onde os registros são publicados. O produtor não o cria.</summary>
    public string Topico { get; set; } = TopicoPadrao;

    /// <summary><c>PLAINTEXT</c>, <c>SSL</c>, <c>SASL_PLAINTEXT</c> ou <c>SASL_SSL</c>. Vazio, o padrão é PLAINTEXT.</summary>
    public string? SecurityProtocol { get; set; }

    /// <summary><c>PLAIN</c>, <c>SCRAM-SHA-256</c> ou <c>SCRAM-SHA-512</c>; obrigatório com SASL.</summary>
    public string? SaslMechanism { get; set; }

    /// <summary>Usuário SASL, com permissão só de escrita no tópico.</summary>
    public string? Usuario { get; set; }

    /// <summary>Senha SASL.</summary>
    public string? Senha { get; set; }

    /// <summary>Caminho do certificado PEM da CA, obrigatório com SSL.</summary>
    public string? CaminhoDoCertificadoCa { get; set; }

    /// <summary>Menor prazo de confirmação aceito.</summary>
    public static readonly TimeSpan PrazoMinimo = TimeSpan.FromMilliseconds(100);

    /// <summary>Prazo para o broker confirmar cada mensagem; vencido, o registro não foi gravado.</summary>
    public TimeSpan PrazoDeConfirmacao { get; set; } = TimeSpan.FromSeconds(10);

    internal ProducerConfig ParaProdutor()
    {
        ProducerConfig produtor = new()
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All,
            AllowAutoCreateTopics = false,
            EnableIdempotence = true,
            MessageTimeoutMs = (int)PrazoDeConfirmacao.TotalMilliseconds,
            ClientId = "uniplus-registro-de-acesso",
        };

        KafkaSecurity.Apply(produtor, ToKafkaSettings());

        return produtor;
    }

    private KafkaSettings ToKafkaSettings() => new()
    {
        SecurityProtocol = SecurityProtocol,
        SaslMechanism = SaslMechanism,
        SaslUsername = Usuario,
        SaslPassword = Senha,
        SslCaLocation = CaminhoDoCertificadoCa,
    };
}

internal sealed class RegistroDeAcessoKafkaOptionsValidator : IValidateOptions<RegistroDeAcessoKafkaOptions>
{
    private const string Section = RegistroDeAcessoKafkaOptions.SectionName;

    private static readonly KafkaSecurityFields Fields = new(
        Section,
        nameof(RegistroDeAcessoKafkaOptions.SecurityProtocol),
        nameof(RegistroDeAcessoKafkaOptions.SaslMechanism),
        nameof(RegistroDeAcessoKafkaOptions.Usuario),
        nameof(RegistroDeAcessoKafkaOptions.Senha),
        nameof(RegistroDeAcessoKafkaOptions.CaminhoDoCertificadoCa),
        CaPem: null);

    public ValidateOptionsResult Validate(string? name, RegistroDeAcessoKafkaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas =
        [
            .. new[] { FalhaDoBootstrap(options), FalhaDoTopico(options), FalhaDoPrazo(options) }.OfType<string>(),
            .. FalhasDeSeguranca(options),
        ];

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static string? FalhaDoBootstrap(RegistroDeAcessoKafkaOptions options) =>
        string.IsNullOrWhiteSpace(options.BootstrapServers)
            ? $"{Section}:BootstrapServers é obrigatório: o registro de acesso a dado pessoal é publicado no Kafka, e sem ele a aplicação não entrega dado protegido."
            : null;

    private static string? FalhaDoTopico(RegistroDeAcessoKafkaOptions options) =>
        string.IsNullOrWhiteSpace(options.Topico) ? $"{Section}:Topico não pode ser vazio." : null;

    // Zero é prazo infinito no cliente Kafka, e o prazo precisa superar o tempo de agrupamento do produtor
    // (5 ms por padrão) e caber em milissegundos inteiros.
    private static string? FalhaDoPrazo(RegistroDeAcessoKafkaOptions options) =>
        options.PrazoDeConfirmacao < RegistroDeAcessoKafkaOptions.PrazoMinimo
            || options.PrazoDeConfirmacao > TimeSpan.FromMilliseconds(int.MaxValue)
            ? $"{Section}:PrazoDeConfirmacao deve estar entre {RegistroDeAcessoKafkaOptions.PrazoMinimo.TotalMilliseconds} ms e {int.MaxValue} ms."
            : null;

    private static List<string> FalhasDeSeguranca(RegistroDeAcessoKafkaOptions options) =>
        KafkaSecurityValidation.Validar(
            Fields,
            options.SecurityProtocol,
            options.SaslMechanism,
            options.Usuario,
            options.Senha,
            options.CaminhoDoCertificadoCa,
            caPem: null);
}
