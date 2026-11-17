namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Confluent.Kafka;

/// <summary>
/// Leitura dos nomes de protocolo e mecanismo da configuração Kafka como os enums
/// <see cref="Confluent.Kafka.SecurityProtocol"/> e <see cref="Confluent.Kafka.SaslMechanism"/>.
/// Aceita formas convencionais (<c>SASL_SSL</c>, <c>SCRAM-SHA-512</c>) e PascalCase
/// (<c>SaslSsl</c>, <c>ScramSha512</c>) — tolera o que vem de variáveis de ambiente
/// no chart Helm e o que aparece em arquivos <c>appsettings*.json</c>.
/// </summary>
internal static class KafkaSecurity
{
    /// <summary>
    /// Aplica <see cref="KafkaSettings"/> ao <see cref="ClientConfig"/> do Confluent.Kafka.
    /// Idempotente — campos vazios ficam como estavam (ClientConfig defaults).
    /// </summary>
    public static void Apply(ClientConfig config, KafkaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);

        ApplyProtocolAndMechanism(config, settings);
        ApplyCredentials(config, settings);
        ApplyCertificateAuthority(config, settings);
    }

    /// <summary>
    /// Protocolo e mecanismo são declarados por nome (PLAINTEXT, SCRAM-SHA-512 etc.), não por inteiro. Rejeita o
    /// número antes de <see cref="Normalize"/>: <c>Enum.TryParse</c> aceita "999" e "-1" e os converte em enum sem
    /// conferir se há nome definido, e como <see cref="Normalize"/> remove o <c>-</c> (para tolerar SCRAM-SHA-512),
    /// "-1" viraria "1" e seria lido como <c>SecurityProtocol.Ssl</c> em silêncio. <c>IsDefined</c> é a defesa em
    /// profundidade contra entradas que driblariam o filtro numérico (caracteres unicode, formatações inesperadas).
    /// </summary>
    public static bool TryParseByName<TEnum>(string? texto, out TEnum valor)
        where TEnum : struct, Enum
    {
        valor = default;
        if (string.IsNullOrWhiteSpace(texto) || long.TryParse(texto.Trim(), out _))
        {
            return false;
        }

        return Enum.TryParse(Normalize(texto), ignoreCase: true, out valor) && Enum.IsDefined(valor);
    }

    private static void ApplyProtocolAndMechanism(ClientConfig cliente, KafkaSettings configuracao)
    {
        if (TryParseByName(configuracao.SecurityProtocol, out SecurityProtocol protocolo))
        {
            cliente.SecurityProtocol = protocolo;
        }

        if (TryParseByName(configuracao.SaslMechanism, out SaslMechanism mecanismo))
        {
            cliente.SaslMechanism = mecanismo;
        }
    }

    private static void ApplyCredentials(ClientConfig cliente, KafkaSettings configuracao)
    {
        if (!string.IsNullOrWhiteSpace(configuracao.SaslUsername))
        {
            cliente.SaslUsername = configuracao.SaslUsername;
        }

        if (!string.IsNullOrWhiteSpace(configuracao.SaslPassword))
        {
            cliente.SaslPassword = configuracao.SaslPassword;
        }
    }

    private static void ApplyCertificateAuthority(ClientConfig cliente, KafkaSettings configuracao)
    {
        if (!string.IsNullOrWhiteSpace(configuracao.SslCaLocation))
        {
            cliente.SslCaLocation = configuracao.SslCaLocation;
        }

        if (!string.IsNullOrWhiteSpace(configuracao.SslCaPem))
        {
            cliente.SslCaPem = configuracao.SslCaPem;
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
