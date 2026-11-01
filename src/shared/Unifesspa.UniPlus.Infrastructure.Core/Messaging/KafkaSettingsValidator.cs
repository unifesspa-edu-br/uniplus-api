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

        List<string> failures = KafkaSecurityValidation.Validar(
            new CamposDeSegurancaKafka(
                KafkaSettings.SectionName,
                nameof(KafkaSettings.SecurityProtocol),
                nameof(KafkaSettings.SaslMechanism),
                nameof(KafkaSettings.SaslUsername),
                nameof(KafkaSettings.SaslPassword),
                nameof(KafkaSettings.SslCaLocation),
                nameof(KafkaSettings.SslCaPem)),
            options.SecurityProtocol,
            options.SaslMechanism,
            options.SaslUsername,
            options.SaslPassword,
            options.SslCaLocation,
            options.SslCaPem);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
