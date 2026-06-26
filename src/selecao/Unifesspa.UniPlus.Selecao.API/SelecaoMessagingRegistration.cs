namespace Unifesspa.UniPlus.Selecao.API;

using System.Diagnostics.CodeAnalysis;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;
using Unifesspa.UniPlus.Selecao.Domain.Events;

using Wolverine;
using Wolverine.Kafka;
using Wolverine.Kafka.Serialization;
using Wolverine.Postgresql;

using EditalPublicadoAvro = unifesspa.uniplus.selecao.events.EditalPublicado;

/// <summary>
/// Wiring de mensageria do módulo Seleção (Kafka/Schema Registry — ADR-0051 — e
/// routing Wolverine — ADR-0003/0004/0005). É setup de processo (uma instância
/// Wolverine por host), então fica fora de <see cref="SelecaoModuleRegistration"/>:
/// tanto o Program.cs standalone quanto o composition root do monólito modular
/// chamam este mesmo método, garantindo paridade de comportamento.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class SelecaoMessagingRegistration
{
    /// <summary>
    /// Registra o Schema Registry do módulo (cliente + schema Avro
    /// <c>edital_events-value</c>) e devolve o configurador de routing Wolverine
    /// do Seleção, para o host compor no <c>configureRouting</c> de
    /// <c>UseWolverineOutboxCascading</c> — sem o host precisar conhecer Kafka/SR.
    /// </summary>
    /// <returns>
    /// Ação que aplica o routing do Seleção a um <see cref="WolverineOptions"/>:
    /// PG queue intra-módulo <c>domain-events</c> + (quando Kafka e SR estão
    /// configurados) tópico Kafka <c>edital_events</c> com serializer Avro Confluent.
    /// </returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "O ISchemaRegistryClient devolvido por RegistrarSchemaRegistry é "
            + "registrado como singleton no DI (ownership transferido ao container, que o "
            + "dispõe no shutdown do IHost). CA2000 não rastreia ownership via DI.")]
    public static Action<WolverineOptions> AddSelecaoMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        ISchemaRegistryClient? srClient = RegistrarSchemaRegistry(services, configuration, environment);

        services.AddSchemaRegistry(configuration)
            .AddSchema(
                subject: "edital_events-value",
                schemaResourceName: EditalPublicadoAvro.SchemaResourceName,
                resourceAssembly: typeof(EditalPublicadoEvent).Assembly);

        return opts => ConfigurarRouting(opts, configuration, srClient);
    }

    /// <summary>
    /// Routing do Seleção: drena <see cref="EditalPublicadoEvent"/> pela PG queue
    /// <c>domain-events</c> (cascading ADR-0005) e, quando Kafka + Schema Registry
    /// estão configurados, projeta o evento Avro para o tópico <c>edital_events</c>.
    /// </summary>
    public static void ConfigurarRouting(
        WolverineOptions opts,
        IConfiguration configuration,
        ISchemaRegistryClient? srClient)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(configuration);

        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        bool kafkaConfigured = !string.IsNullOrWhiteSpace(configuration["Kafka:BootstrapServers"]);
        if (kafkaConfigured && srClient is not null)
        {
            // Kafka + Schema Registry: produz Avro com schema-id no envelope
            // (Confluent wire format). Consumers cross-módulo recuperam o schema
            // via schema-id, sem dependência do assembly Selecao.Domain.
            opts.PublishMessage<EditalPublicadoAvro>()
                .ToKafkaTopic("edital_events")
                .DefaultSerializer(new SchemaRegistryAvroSerializer(srClient));
        }
    }

    /// <summary>
    /// Cria o <see cref="ISchemaRegistryClient"/> a partir de
    /// <c>SchemaRegistry:Url</c> (ou <see langword="null"/> quando vazio) e o
    /// registra como singleton, aplicando a invariante ADR-0051/0053 (Kafka em
    /// ambiente produtivo exige Schema Registry).
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "O cliente criado por CreateClient é registrado como singleton no DI "
            + "logo em seguida (services.AddSingleton); o container assume ownership e o dispõe "
            + "no shutdown do IHost. CA2000 não rastreia ownership via DI.")]
    private static ISchemaRegistryClient? RegistrarSchemaRegistry(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        SchemaRegistrySettings srSettings = configuration
            .GetSection(SchemaRegistrySettings.SectionName)
            .Get<SchemaRegistrySettings>() ?? new SchemaRegistrySettings();

        // Invariante operacional (ADR-0051/0053): Kafka habilitado exige Schema
        // Registry em ambientes produtivos. Só Development afrouxa (cobre test
        // factories que sobem com UseEnvironment("Development")).
        bool kafkaEnabled = !string.IsNullOrWhiteSpace(configuration["Kafka:BootstrapServers"]);
        bool srMissing = string.IsNullOrWhiteSpace(srSettings.Url);
        if (kafkaEnabled && srMissing)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Configuração inválida: Kafka:BootstrapServers populado mas SchemaRegistry:Url vazio em "
                    + $"ASPNETCORE_ENVIRONMENT={environment.EnvironmentName}. "
                    + "ADR-0051 exige Schema Registry para todo publishing cross-módulo em ambientes produtivos. "
                    + "Configure SchemaRegistry:Url (ou desligue Kafka apagando Kafka:BootstrapServers).");
            }

            using ILoggerFactory missingSrLoggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
            Microsoft.Extensions.Logging.ILogger missingSrLogger =
                missingSrLoggerFactory.CreateLogger("Selecao.Messaging.Bootstrap");
#pragma warning disable CA1848 // Bootstrap logging — fora do hot path; LoggerMessage source generator overkill aqui.
#pragma warning disable CA2254 // Mensagem fixa após format de string interpolada — sem placeholders dinâmicos.
            missingSrLogger.LogWarning(
                "Kafka habilitado sem Schema Registry em ambiente {Env} — publishing Avro cross-módulo desligado. "
                + "Esperado em test factory que isola cascading puro; sinal de bug em ambiente produtivo (ADR-0051).",
                environment.EnvironmentName);
#pragma warning restore CA2254
#pragma warning restore CA1848
        }

        if (string.IsNullOrWhiteSpace(srSettings.Url))
        {
            return null;
        }

        using ILoggerFactory bootstrapLoggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
        ISchemaRegistryClient srClient = SchemaRegistryServiceCollectionExtensions.CreateClient(
            srSettings,
            bootstrapLoggerFactory,
            services);
        services.AddSingleton(srClient);
        return srClient;
    }
}
