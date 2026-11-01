namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Confluent.Kafka;

/// <summary>Nomes dos campos de segurança numa seção de configuração, para as mensagens de validação.</summary>
internal sealed record CamposDeSegurancaKafka(
    string Secao,
    string Protocolo,
    string Mecanismo,
    string Usuario,
    string Senha,
    string CaLocation,
    string? CaPem);

/// <summary>
/// Validação cruzada de segurança do cliente Kafka (protocolo, SASL, SSL), a mesma para todo uso que conecta ao broker:
/// o transporte do Wolverine e o registro de acesso. Cada um informa os nomes dos próprios campos.
/// </summary>
internal static class KafkaSecurityValidation
{
    public static List<string> Validar(
        CamposDeSegurancaKafka campos,
        string? securityProtocol,
        string? saslMechanism,
        string? usuario,
        string? senha,
        string? caLocation,
        string? caPem)
    {
        string secao = campos.Secao;
        List<string> failures = [];

        bool hasAnySaslField =
            !string.IsNullOrWhiteSpace(saslMechanism)
            || !string.IsNullOrWhiteSpace(usuario)
            || !string.IsNullOrWhiteSpace(senha);

        bool hasAnySslField = !string.IsNullOrWhiteSpace(caLocation) || !string.IsNullOrWhiteSpace(caPem);

        SecurityProtocol protocol = SecurityProtocol.Plaintext;
        if (!string.IsNullOrWhiteSpace(securityProtocol)
            && !KafkaSecurity.TryParseSecurityProtocol(securityProtocol, out protocol))
        {
            failures.Add($"{secao}:{campos.Protocolo} '{securityProtocol}' inválido. Use PLAINTEXT, SSL, SASL_PLAINTEXT ou SASL_SSL.");
            return failures;
        }

        bool requiresSasl = protocol is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl;
        bool requiresSsl = protocol is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl;

        // Coerência: campos SASL preenchidos exigem protocolo SASL_*; idem para SSL.
        // Sem isso, credenciais com SecurityProtocol=PLAINTEXT fariam o cliente cair em PLAINTEXT
        // silenciosamente — risco de segurança.
        if (hasAnySaslField && !requiresSasl)
        {
            failures.Add($"{secao}:{campos.Mecanismo}/{campos.Usuario}/{campos.Senha} exigem {campos.Protocolo}=SASL_PLAINTEXT ou SASL_SSL.");
        }

        if (hasAnySslField && !requiresSsl)
        {
            string ca = campos.CaPem is null ? campos.CaLocation : $"{campos.CaLocation}/{campos.CaPem}";
            failures.Add($"{secao}:{ca} exigem {campos.Protocolo}=SSL ou SASL_SSL.");
        }

        // CaLocation e CaPem são alternativas — preencher os dois é ambíguo.
        if (campos.CaPem is not null
            && !string.IsNullOrWhiteSpace(caLocation)
            && !string.IsNullOrWhiteSpace(caPem))
        {
            failures.Add($"{secao}:{campos.CaLocation} e {secao}:{campos.CaPem} são mutuamente exclusivos — preencha apenas um.");
        }

        if (requiresSasl)
        {
            if (string.IsNullOrWhiteSpace(saslMechanism))
            {
                failures.Add($"{secao}:{campos.Mecanismo} é obrigatório com {campos.Protocolo}={securityProtocol}.");
            }
            else if (!KafkaSecurity.TryParseSaslMechanism(saslMechanism, out SaslMechanism mechanism))
            {
                failures.Add($"{secao}:{campos.Mecanismo} '{saslMechanism}' inválido. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.");
            }
            else
            {
                // PLAIN e SCRAM-* exigem user+pwd. OAUTHBEARER e GSSAPI ficam fora do escopo desta camada —
                // operador escolhendo um destes precisa estender o helper, não cair no caminho user/password
                // silenciosamente.
                bool requiresUserPassword = mechanism is
                    SaslMechanism.Plain
                    or SaslMechanism.ScramSha256
                    or SaslMechanism.ScramSha512;

                if (!requiresUserPassword)
                {
                    failures.Add($"{secao}:{campos.Mecanismo} '{saslMechanism}' não é suportado pelo Uni+. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(usuario))
                    {
                        failures.Add($"{secao}:{campos.Usuario} é obrigatório com {campos.Protocolo}={securityProtocol}.");
                    }

                    if (string.IsNullOrWhiteSpace(senha))
                    {
                        failures.Add($"{secao}:{campos.Senha} é obrigatório com {campos.Protocolo}={securityProtocol}.");
                    }
                }
            }
        }

        // Política Uni+ standalone (ADR-009 do uniplus-infra): cluster usa CA self-signed, então CA explícito é
        // mandatório quando o transporte é SSL/SASL_SSL.
        if (requiresSsl && string.IsNullOrWhiteSpace(caLocation) && string.IsNullOrWhiteSpace(caPem))
        {
            string ca = campos.CaPem is null ? campos.CaLocation : $"{campos.CaLocation} ou {secao}:{campos.CaPem}";
            failures.Add($"{secao}:{ca} é obrigatório com {campos.Protocolo}={securityProtocol} (política Uni+: CA explícito).");
        }

        return failures;
    }
}
