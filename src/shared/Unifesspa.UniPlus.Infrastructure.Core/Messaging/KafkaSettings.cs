namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Bound options for the Kafka transport used by Wolverine. Mapeia a seção <c>Kafka:</c> de
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Configurações suportadas:
/// </para>
/// <list type="bullet">
///   <item><description><b>PLAINTEXT</b> (Development / docker-compose): apenas <see cref="BootstrapServers"/>.</description></item>
///   <item><description><b>SASL_SSL + SCRAM-SHA-512</b> (standalone, ADR-009 do uniplus-infra):
///     <see cref="BootstrapServers"/> + <see cref="SecurityProtocol"/>=<c>SaslSsl</c> +
///     <see cref="SaslMechanism"/>=<c>ScramSha512</c> + <see cref="SaslUsername"/> +
///     <see cref="SaslPassword"/> + <see cref="SslCaLocation"/> ou <see cref="SslCaPem"/>.</description></item>
///   <item><description><b>SASL_PLAINTEXT</b> e <b>SSL</b>: também suportados — validação cobre os
///     pré-requisitos por protocolo.</description></item>
/// </list>
/// <para>
/// Para a lista de valores aceitos consultar
/// <see cref="Confluent.Kafka.SecurityProtocol"/> e <see cref="Confluent.Kafka.SaslMechanism"/>.
/// Os valores são lidos como string e convertidos via <see cref="System.Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
/// (case-insensitive); aceita formas <c>SASL_SSL</c>, <c>SaslSsl</c>, <c>SCRAM-SHA-512</c>,
/// <c>ScramSha512</c>.
/// </para>
/// </remarks>
public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    /// <summary>Lista de bootstrap servers (<c>host1:port,host2:port</c>). Vazio desliga o transporte Kafka.</summary>
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>
    /// Protocolo de segurança (<c>PLAINTEXT</c>, <c>SSL</c>, <c>SASL_PLAINTEXT</c>, <c>SASL_SSL</c>).
    /// Quando vazio/ausente, o cliente usa <c>PLAINTEXT</c> default — comportamento backward-compat
    /// de Development com docker-compose.
    /// </summary>
    public string? SecurityProtocol { get; init; }

    /// <summary>
    /// Mecanismo SASL (<c>PLAIN</c>, <c>SCRAM-SHA-256</c>, <c>SCRAM-SHA-512</c>, <c>OAUTHBEARER</c>, <c>GSSAPI</c>).
    /// Obrigatório quando <see cref="SecurityProtocol"/> envolve SASL.
    /// </summary>
    public string? SaslMechanism { get; init; }

    /// <summary>Usuário SASL — obrigatório com SASL_*.</summary>
    public string? SaslUsername { get; init; }

    /// <summary>Senha SASL — obrigatória com SASL_*. Sempre via env var/Vault.</summary>
    public string? SaslPassword { get; init; }

    /// <summary>
    /// Path para o arquivo PEM do CA (ex.: <c>/etc/uniplus-kafka/ca.crt</c>) — obrigatório com SSL ou SASL_SSL,
    /// a menos que <see cref="SslCaPem"/> esteja preenchido.
    /// </summary>
    public string? SslCaLocation { get; init; }

    /// <summary>
    /// CA PEM inline. Alternativa a <see cref="SslCaLocation"/> — útil quando o cert é injetado
    /// diretamente via env var em vez de volume mount.
    /// </summary>
    public string? SslCaPem { get; init; }
}
