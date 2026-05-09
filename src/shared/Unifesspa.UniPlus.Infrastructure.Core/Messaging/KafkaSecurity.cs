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

        // Rejeita inputs numéricos antes de Normalize — Enum.TryParse aceita "999"/"-1"/"1"
        // e casts os valores como enum sem verificar se mapeiam para nome definido. Pior ainda,
        // Normalize() strip `-` (para tolerar SCRAM-SHA-512), então "-1" viraria "1" e parseia
        // para SecurityProtocol.Ssl silenciosamente. A política Uni+ é: SecurityProtocol é
        // declarado por nome (PLAINTEXT, SSL, SASL_PLAINTEXT, SASL_SSL), não por inteiro.
        if (long.TryParse(raw.Trim(), out _))
        {
            return false;
        }

        string normalized = Normalize(raw);

        // IsDefined garante que só nomes legítimos do enum passem, defesa em profundidade contra
        // inputs que driblariam o filtro numérico (caracteres unicode, formatações inesperadas).
        return Enum.TryParse(normalized, ignoreCase: true, out protocol)
            && Enum.IsDefined(protocol);
    }

    public static bool TryParseSaslMechanism(string? raw, out SaslMechanism mechanism)
    {
        mechanism = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Mesma proteção de TryParseSecurityProtocol — Mechanism é declarado por nome (PLAIN,
        // SCRAM-SHA-512 etc.), não por inteiro.
        if (long.TryParse(raw.Trim(), out _))
        {
            return false;
        }

        string normalized = Normalize(raw);

        return Enum.TryParse(normalized, ignoreCase: true, out mechanism)
            && Enum.IsDefined(mechanism);
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
