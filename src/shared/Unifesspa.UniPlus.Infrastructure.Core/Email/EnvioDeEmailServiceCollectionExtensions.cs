namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Email;

using Wolverine;
using Wolverine.Postgresql;

public static class EnvioDeEmailServiceCollectionExtensions
{
    /// <summary>
    /// Fila PostgreSQL durável dos pedidos de e-mail: o envelope entra no outbox na mesma
    /// transação de quem o publicou (ADR-0004), então a mensagem sobrevive a queda do relay e do
    /// processo. Fila local do Wolverine não serve: é em memória.
    /// </summary>
    public const string FilaDeEnvio = "email-envio";

    /// <summary>
    /// Registra o transporte SMTP e valida <c>Email:Smtp</c> na subida: sem relay configurado a
    /// API não sobe, em vez de aceitar pedidos que nunca serão entregues.
    /// </summary>
    public static IServiceCollection AddEnvioDeEmail(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EmailSmtpOptions>()
            .Bind(configuration.GetSection(EmailSmtpOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailSmtpOptions>, EmailSmtpOptionsValidator>());
        services.TryAddSingleton<ITransporteDeEmail, SmtpTransporteDeEmail>();

        return services;
    }

    /// <summary>Roteia <see cref="EnviarEmail"/> para a fila durável e a consome neste processo.</summary>
    public static void ConfigurarRoutingDeEmail(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        // Explícito, e não pela varredura do assembly compartilhado: o handler só pode existir nos
        // hosts que registram o transporte (AddEnvioDeEmail).
        opts.Discovery.IncludeType<EntregaDeEmail>();
        opts.PublishMessage<EnviarEmail>().ToPostgresqlQueue(FilaDeEnvio);
        opts.ListenToPostgresqlQueue(FilaDeEnvio);
    }
}
