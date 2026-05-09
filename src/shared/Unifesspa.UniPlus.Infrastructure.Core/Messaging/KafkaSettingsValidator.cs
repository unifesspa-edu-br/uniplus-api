namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Microsoft.Extensions.Options;

/// <summary>
/// Validação cross-field de <see cref="KafkaSettings"/>.
/// </summary>
/// <remarks>
/// Garante coerência: campos SASL/SSL preenchidos só fazem sentido sob protocolo correspondente
/// (rejeita o caso onde credenciais são informadas mas o cliente acabaria em PLAINTEXT, com
/// risco de "parecer autenticado" sem estar). Para SASL_PLAINTEXT/SASL_SSL, exige mecanismo
/// + credentials por mecanismo: PLAIN/SCRAM-* requerem usuário+senha; OAUTHBEARER/GSSAPI não
/// são suportados nesta camada — failure explícita orienta o operador.
/// </remarks>
public sealed class KafkaSettingsValidator : IValidateOptions<KafkaSettings>
{
    public ValidateOptionsResult Validate(string? name, KafkaSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Bootstrap vazio = transporte desligado (Development sem Kafka). Nada a validar.
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        bool hasAnySaslField =
            !string.IsNullOrWhiteSpace(options.SaslMechanism)
            || !string.IsNullOrWhiteSpace(options.SaslUsername)
            || !string.IsNullOrWhiteSpace(options.SaslPassword);

        bool hasAnySslField =
            !string.IsNullOrWhiteSpace(options.SslCaLocation)
            || !string.IsNullOrWhiteSpace(options.SslCaPem);

        Confluent.Kafka.SecurityProtocol protocol = Confluent.Kafka.SecurityProtocol.Plaintext;
        bool protocolExplicit = !string.IsNullOrWhiteSpace(options.SecurityProtocol);

        if (protocolExplicit
            && !KafkaSecurity.TryParseSecurityProtocol(options.SecurityProtocol, out protocol))
        {
            failures.Add($"Kafka:SecurityProtocol '{options.SecurityProtocol}' inválido. Use PLAINTEXT, SSL, SASL_PLAINTEXT ou SASL_SSL.");
            return ValidateOptionsResult.Fail(failures);
        }

        bool requiresSasl = protocol is Confluent.Kafka.SecurityProtocol.SaslPlaintext or Confluent.Kafka.SecurityProtocol.SaslSsl;
        bool requiresSsl = protocol is Confluent.Kafka.SecurityProtocol.Ssl or Confluent.Kafka.SecurityProtocol.SaslSsl;

        // Coerência: campos SASL preenchidos exigem protocolo SASL_*; idem para SSL.
        // Sem isso, configurações como SaslUsername="x" + SecurityProtocol=PLAINTEXT
        // fariam o cliente cair em PLAINTEXT silenciosamente — risco de segurança.
        if (hasAnySaslField && !requiresSasl)
        {
            failures.Add("Kafka:SaslMechanism/SaslUsername/SaslPassword exigem SecurityProtocol=SASL_PLAINTEXT ou SASL_SSL.");
        }

        if (hasAnySslField && !requiresSsl)
        {
            failures.Add("Kafka:SslCaLocation/SslCaPem exigem SecurityProtocol=SSL ou SASL_SSL.");
        }

        // SslCaLocation e SslCaPem são alternativas — preencher os dois é ambíguo.
        if (!string.IsNullOrWhiteSpace(options.SslCaLocation)
            && !string.IsNullOrWhiteSpace(options.SslCaPem))
        {
            failures.Add("Kafka:SslCaLocation e Kafka:SslCaPem são mutuamente exclusivos — preencha apenas um.");
        }

        if (requiresSasl)
        {
            if (string.IsNullOrWhiteSpace(options.SaslMechanism))
            {
                failures.Add($"Kafka:SaslMechanism é obrigatório com SecurityProtocol={options.SecurityProtocol}.");
            }
            else if (!KafkaSecurity.TryParseSaslMechanism(options.SaslMechanism, out Confluent.Kafka.SaslMechanism mechanism))
            {
                failures.Add($"Kafka:SaslMechanism '{options.SaslMechanism}' inválido. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.");
            }
            else
            {
                // Por mecanismo: PLAIN e SCRAM-* exigem user+pwd. OAUTHBEARER e GSSAPI ficam fora
                // do escopo desta camada — operador escolhendo um destes precisa estender o
                // helper, não cair no caminho user/password silenciosamente.
                bool requiresUserPassword = mechanism is
                    Confluent.Kafka.SaslMechanism.Plain
                    or Confluent.Kafka.SaslMechanism.ScramSha256
                    or Confluent.Kafka.SaslMechanism.ScramSha512;

                if (!requiresUserPassword)
                {
                    failures.Add($"Kafka:SaslMechanism '{options.SaslMechanism}' não é suportado pelo Uni+. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(options.SaslUsername))
                    {
                        failures.Add($"Kafka:SaslUsername é obrigatório com SecurityProtocol={options.SecurityProtocol}.");
                    }

                    if (string.IsNullOrWhiteSpace(options.SaslPassword))
                    {
                        failures.Add($"Kafka:SaslPassword é obrigatório com SecurityProtocol={options.SecurityProtocol}.");
                    }
                }
            }
        }

        // Política Uni+ standalone (ADR-009 do uniplus-infra): cluster usa CA self-signed,
        // então CA explícito (location ou PEM) é mandatório quando o transporte é SSL/SASL_SSL.
        if (requiresSsl
            && string.IsNullOrWhiteSpace(options.SslCaLocation)
            && string.IsNullOrWhiteSpace(options.SslCaPem))
        {
            failures.Add($"Kafka:SslCaLocation ou Kafka:SslCaPem é obrigatório com SecurityProtocol={options.SecurityProtocol} (política Uni+: CA explícito).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
