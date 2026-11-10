namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;

using Unifesspa.UniPlus.Application.Abstractions.Email;

using Wolverine;
using Wolverine.Postgresql;
using Wolverine.Runtime;

public static class EnvioDeEmailServiceCollectionExtensions
{
    /// <summary>
    /// Fila PostgreSQL durável dos pedidos de e-mail: o envelope entra no outbox na mesma
    /// transação de quem o publicou (ADR-0004), então a mensagem sobrevive a queda do relay e do
    /// processo. Fila local do Wolverine não serve: é em memória.
    /// </summary>
    public const string FilaDeEnvio = "email-envio";

    /// <summary>A connection string do banco do outbox, onde a fila e as mortas são lidas.</summary>
    public const string ConexaoDoOutbox = "UniPlusDb";

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
        AddMonitoramento(services);

        return services;
    }

    // A fila e as mortas vivem no banco do outbox: o retrato lê dele, e o medidor entra no pipeline de métricas.
    private static void AddMonitoramento(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MetricasDaFilaDeEmail>();
        services.AddSingleton(sp => new LeituraDaFilaDeEmail(
            sp.GetRequiredService<IConfiguration>().GetConnectionString(ConexaoDoOutbox)
                ?? throw new InvalidOperationException($"Connection string '{ConexaoDoOutbox}' não configurada."),
            TabelasDoEnvioDeEmail.Do(sp.GetRequiredService<IWolverineRuntime>())));
        services.AddSingleton<AmostragemDaFilaDeEmail>();
        services.AddHostedService<AmostragemDaFilaDeEmailHostedService>();

        // O medidor nasce antes do provedor de métricas: a série dos instantes existe desde a primeira coleta.
        services.ConfigureOpenTelemetryMeterProvider(static (sp, builder) =>
            builder.AddMeter(sp.GetRequiredService<MetricasDaFilaDeEmail>().Medidor.Name));
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
