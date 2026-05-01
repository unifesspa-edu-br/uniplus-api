namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Kafka;
using Wolverine.Postgresql;

/// <summary>
/// Configuração canônica do backbone Wolverine produtivo do UniPlus, conforme
/// ADR-0004 (outbox transacional) e ADR-0005 (cascading messages como drenagem
/// canônica de domain events). Aplicada por <see cref="UseWolverineOutboxCascading"/>
/// nos hosts de cada módulo (Selecao.API, Ingresso.API).
/// </summary>
public static class WolverineOutboxConfiguration
{
    /// <summary>
    /// Schema PostgreSQL onde Wolverine cria as tabelas de outbox/inbox/scheduled
    /// (<c>wolverine_outgoing_envelopes</c>, <c>wolverine_incoming_envelopes</c>,
    /// <c>wolverine_node_assignments</c> etc.).
    /// </summary>
    public const string PersistenceSchema = "wolverine";

    /// <summary>
    /// Chave em <see cref="IConfiguration"/> de onde o bootstrap servers Kafka é
    /// lido por padrão. Quando o valor é nulo, vazio ou whitespace, o transporte
    /// Kafka não é registrado — útil para ambientes locais e testes que só
    /// exercitam a queue PG.
    /// </summary>
    public const string DefaultKafkaConfigKey = "Kafka:BootstrapServers";

    /// <summary>
    /// Configura o host com Wolverine + outbox transacional Postgres + (opcional)
    /// transporte Kafka. A drenagem de domain events é feita por cascading
    /// messages (handlers retornam <c>IEnumerable&lt;object&gt;</c>) — sem
    /// <c>PublishDomainEventsFromEntityFrameworkCore</c>, conforme ADR-0005.
    /// </summary>
    /// <param name="host">Host do <see cref="WebApplicationBuilder"/>.</param>
    /// <param name="configuration"><see cref="IConfiguration"/> live do
    /// <see cref="WebApplicationBuilder"/> (passar <c>builder.Configuration</c>).
    /// A leitura da connection string acontece dentro do callback do
    /// <c>UseWolverine</c>, momento em que os providers já foram materializados
    /// — overrides aplicados via env vars ou <c>WebApplicationFactory</c> são
    /// respeitados, desde que entrem em sources que o
    /// <see cref="WebApplicationBuilder"/> consulta (env vars sempre entram;
    /// <c>ConfigureAppConfiguration</c> via <c>IWebHostBuilder</c> não se
    /// propaga para <see cref="WebApplicationBuilder.Configuration"/> em apps
    /// minimal API — usar env vars nos testes).</param>
    /// <param name="connectionStringName">Nome da connection string em
    /// <see cref="IConfiguration.GetConnectionString"/> (ex.: <c>"SelecaoDb"</c>,
    /// <c>"IngressoDb"</c>).</param>
    /// <param name="kafkaConfigKey">Chave em <see cref="IConfiguration"/> onde o
    /// bootstrap servers Kafka é lido. Se ausente/whitespace, o transporte Kafka
    /// é desligado mantendo a queue PG ativa. Padrão:
    /// <see cref="DefaultKafkaConfigKey"/>.</param>
    /// <param name="configureRouting">Callback para roteamento específico do
    /// módulo (ex.: <c>opts.PublishMessage&lt;EditalPublicadoEvent&gt;()
    /// .ToPostgresqlQueue("domain-events")</c>). Executado depois das policies
    /// transacionais; pode adicionar publishers, listeners, dead letters etc.
    /// Pode ser nulo se o módulo ainda não tem eventos a rotear.</param>
    public static IHostBuilder UseWolverineOutboxCascading(
        this IHostBuilder host,
        IConfiguration configuration,
        string connectionStringName,
        string kafkaConfigKey = DefaultKafkaConfigKey,
        Action<WolverineOptions>? configureRouting = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
        ArgumentException.ThrowIfNullOrWhiteSpace(kafkaConfigKey);

        return host.UseWolverine(opts =>
        {
            string? connectionString = configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{connectionStringName}' não configurada — Wolverine não pode inicializar o outbox.");
            }

            // Persistência do outbox no mesmo banco do módulo, schema isolado.
            // EnableMessageTransport(_ => { }) ativa o transporte interno PG queue.
            opts.PersistMessagesWithPostgresql(connectionString, PersistenceSchema)
                .EnableMessageTransport(_ => { });

            // Atomicidade write+evento: o handler que muta agregado e retorna
            // cascading messages tem o envelope persistido na MESMA transação do
            // SaveChanges, via IEnvelopeTransaction instalado por
            // EnrollDbContextInTransaction.
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.AutoApplyTransactions();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

            // Middleware CQRS canônicos (logging + validação FluentValidation),
            // restritos a chains de ICommand<>/IQuery<> — mensagens internas do
            // Wolverine não atravessam esse pipeline.
            opts.AddCommandQueryMiddleware();

            // Schema do Wolverine NÃO é auto-criado em runtime nesta camada
            // produtiva — provisioning é responsabilidade do deploy. Testes
            // integrados controlam o schema via fixture explícita; ver
            // CascadingFixture no projeto de testes.

            string? kafkaBootstrapServers = configuration[kafkaConfigKey];
            if (!string.IsNullOrWhiteSpace(kafkaBootstrapServers))
            {
                opts.UseKafka(kafkaBootstrapServers).AutoProvision();
            }

            configureRouting?.Invoke(opts);
        });
    }
}
