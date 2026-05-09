namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Confluent.Kafka;

/// <summary>
/// Helpers internos para mapear strings de configuração Kafka para os enums
/// <see cref="Confluent.Kafka.SecurityProtocol"/> e <see cref="Confluent.Kafka.SaslMechanism"/>.
/// Aceita formas convencionais (<c>SASL_SSL</c>, <c>SCRAM-SHA-512</c>) e PascalCase
/// (<c>SaslSsl</c>, <c>ScramSha512</c>) — tolera o que vem de variáveis de ambiente
/// no chart Helm e o que aparece em arquivos <c>appsettings*.json</c>.
/// </summary>
internal static class KafkaSecurity
{
    public static bool TryParseSecurityProtocol(string? raw, out SecurityProtocol protocol)
    {
        protocol = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = Normalize(raw);
        return Enum.TryParse(normalized, ignoreCase: true, out protocol);
    }

    public static bool TryParseSaslMechanism(string? raw, out SaslMechanism mechanism)
    {
        mechanism = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = Normalize(raw);
        return Enum.TryParse(normalized, ignoreCase: true, out mechanism);
    }

    /// <summary>
    /// Aplica <see cref="KafkaSettings"/> ao <see cref="ClientConfig"/> do Confluent.Kafka.
    /// Idempotente — campos vazios ficam como estavam (ClientConfig defaults).
    /// </summary>
    public static void Apply(ClientConfig config, KafkaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);

        if (TryParseSecurityProtocol(settings.SecurityProtocol, out SecurityProtocol protocol))
        {
            config.SecurityProtocol = protocol;
        }

        if (TryParseSaslMechanism(settings.SaslMechanism, out SaslMechanism mechanism))
        {
            config.SaslMechanism = mechanism;
        }

        if (!string.IsNullOrWhiteSpace(settings.SaslUsername))
        {
            config.SaslUsername = settings.SaslUsername;
        }

        if (!string.IsNullOrWhiteSpace(settings.SaslPassword))
        {
            config.SaslPassword = settings.SaslPassword;
        }

        if (!string.IsNullOrWhiteSpace(settings.SslCaLocation))
        {
            config.SslCaLocation = settings.SslCaLocation;
        }

        if (!string.IsNullOrWhiteSpace(settings.SslCaPem))
        {
            config.SslCaPem = settings.SslCaPem;
        }
    }

    /// <summary>
    /// <c>SASL_SSL</c> → <c>SaslSsl</c>; <c>SCRAM-SHA-512</c> → <c>ScramSha512</c>;
    /// <c>OAUTHBEARER</c> → <c>OAuthBearer</c> (case-folded depois pelo TryParse ignoreCase).
    /// </summary>
    private static string Normalize(string raw) =>
        raw.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
}
