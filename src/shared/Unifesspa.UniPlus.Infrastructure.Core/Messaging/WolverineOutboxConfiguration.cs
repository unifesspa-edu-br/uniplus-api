namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Middleware;

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
    /// Seção em <see cref="IConfiguration"/> de onde <see cref="KafkaSettings"/> é lida
    /// por padrão. Quando <see cref="KafkaSettings.BootstrapServers"/> é vazio, o transporte
    /// Kafka não é registrado — útil para ambientes locais e testes que só exercitam a queue PG.
    /// </summary>
    public const string DefaultKafkaConfigSection = KafkaSettings.SectionName;


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
    /// <param name="kafkaConfigSection">Seção em <see cref="IConfiguration"/> ligada a
    /// <see cref="KafkaSettings"/>. Se <see cref="KafkaSettings.BootstrapServers"/> for vazio,
    /// o transporte Kafka é desligado mantendo a queue PG ativa. Padrão:
    /// <see cref="DefaultKafkaConfigSection"/>.</param>
    /// <param name="configureRouting">Callback para roteamento específico do
    /// módulo (ex.: <c>opts.PublishMessage&lt;EditalPublicadoEvent&gt;()
    /// .ToPostgresqlQueue("domain-events")</c>). Executado depois das policies
    /// transacionais; pode adicionar publishers, listeners, dead letters etc.
    /// Pode ser nulo se o módulo ainda não tem eventos a rotear.</param>
    /// <remarks>
    /// <para><strong>Nota sobre <c>Discovery.IncludeAssembly</c> (issue #198):</strong>
    /// hoje o consumidor único (<c>Selecao.API/Program.cs</c>) chama
    /// <c>opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly)</c>
    /// inline dentro do <paramref name="configureRouting"/>. Quando Ingresso
    /// ganhar o primeiro handler real do tipo cascading, refatorar este
    /// helper para receber um parâmetro adicional
    /// <c>params Type[] applicationMarkers</c> (ou
    /// <c>IEnumerable&lt;Assembly&gt;</c>) que faça o
    /// <c>opts.Discovery.IncludeAssembly</c> internamente. Não antecipar a
    /// abstração agora — YAGNI até existir o segundo consumidor.</para>
    /// </remarks>
    public static IHostBuilder UseWolverineOutboxCascading(
        this IHostBuilder host,
        IConfiguration configuration,
        string connectionStringName,
        string kafkaConfigSection = DefaultKafkaConfigSection,
        Action<WolverineOptions>? configureRouting = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
        ArgumentException.ThrowIfNullOrWhiteSpace(kafkaConfigSection);

        // Guarda contra a forma legada exata deste parâmetro (até o #343 era a chave inteira do
        // bootstrap — `"Kafka:BootstrapServers"` — definida na const `DefaultKafkaConfigKey`).
        // Bind silencioso dessa chave terminal como se fosse o nome de uma seção devolveria um
        // `KafkaSettings` vazio e desligaria o transporte sem aviso. Restringimos a guarda ao
        // sufixo `:BootstrapServers` para preservar paths legítimos de seção aninhada
        // (ex.: `"Messaging:Kafka"`), que `IConfiguration.GetSection` suporta nativamente.
        if (kafkaConfigSection.EndsWith(":BootstrapServers", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"kafkaConfigSection '{kafkaConfigSection}' parece a forma legada do parâmetro (chave terminal `Kafka:BootstrapServers`). "
                + "Passe o nome da seção (ex.: 'Kafka' ou 'Messaging:Kafka').",
                nameof(kafkaConfigSection));
        }

        // Bind + valida KafkaSettings via DI/IOptions. ValidateOnStart roda no host start,
        // antes de qualquer hosted service consumir o Kafka. Mensagens de validação reportadas
        // no formato canônico do .NET (OptionsValidationException com lista de campos faltantes).
        host.ConfigureServices(services =>
        {
            services.AddOptions<KafkaSettings>()
                .Bind(configuration.GetSection(kafkaConfigSection))
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<KafkaSettings>, KafkaSettingsValidator>();
        });

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

            // CorrelationIdEnvelopeMiddleware roda em TODOS os chains (inclusive
            // event handlers consumidos de Kafka) — implementa o terceiro componente
            // da ADR-0052. Registrado ANTES do AddCommandQueryMiddleware para que o
            // escopo do LogContext esteja ativo quando o WolverineLoggingMiddleware
            // emite a entrada inicial "Processando {RequestName}".
            opts.AddCorrelationIdMiddleware();

            // Middleware CQRS canônicos (logging + validação FluentValidation),
            // restritos a chains de ICommand<>/IQuery<> — mensagens internas do
            // Wolverine não atravessam esse pipeline.
            opts.AddCommandQueryMiddleware();

            // Propaga o header `uniplus.correlation-id` de qualquer envelope incoming
            // (HTTP → Send, ou outbox/Kafka → consumer) para todas as outgoing messages
            // do mesmo handler context — cascading, IMessageBus.PublishAsync, agendamentos.
            // É o que fecha a propagação do CorrelationId via Kafka exigida pela ADR-0052.
            // O CorrelationIdEnvelopeMiddleware acima garante que o header esteja sempre
            // presente no incoming envelope (gera GUID caso ausente/inválido).
            opts.Policies.PropagateIncomingHeaderToOutgoing(CorrelationIdEnvelopeMiddleware.HeaderName);

            // Discovery do assembly Infrastructure.Core — handlers compartilhados (ex.:
            // SmokePingHandler que processa SmokePingMessage publicada pelo endpoint
            // /api/_smoke/messaging/publish do #346) ficam descobertos sem que cada
            // Program.cs precise repetir o IncludeAssembly. Idempotente — assembly do
            // entry já é scaneado por default; este registro é defensivo para handlers
            // do Core.
            opts.Discovery.IncludeAssembly(typeof(WolverineOutboxConfiguration).Assembly);

            // Schema do Wolverine (tabelas wolverine_outgoing_envelopes, wolverine_incoming_envelopes,
            // wolverine_node_assignments etc.) é auto-criado/atualizado no startup — issue #344.
            // Idempotente: Wolverine inspeciona o schema atual e aplica apenas o delta. Múltiplas
            // réplicas startando simultaneamente são coordenadas pelo lock interno do framework.
            //
            // Decisão (#344): em ambientes Uni+ não há orquestração de schema externa ao host
            // (sem step de "dotnet ef database update" no Helm chart), então delegar a criação
            // ao próprio host é o caminho mais simples e racional para destravar bring-up de
            // pods com banco vazio (standalone/lab). EF Core migrations dos módulos são
            // aplicadas em paralelo por ApplyMigrationsAsync<TContext> no Program.cs.
            opts.AutoBuildMessageStorageOnStartup = JasperFx.AutoCreate.CreateOrUpdate;

            // Lê a seção uma única vez aqui — ValidateOnStart no DI não atinge este callback
            // (UseWolverine roda no Build, antes de StartAsync). Validação inline replica
            // as regras essenciais para falhar antes do AutoProvision tentar conectar com
            // config inválida; o IValidateOptions registrado acima garante o mesmo erro
            // padronizado se outro consumidor resolver IOptions<KafkaSettings>.
            KafkaSettings kafkaSettings = configuration.GetSection(kafkaConfigSection).Get<KafkaSettings>()
                ?? new KafkaSettings();

            if (string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
            {
                configureRouting?.Invoke(opts);
                return;
            }

            ValidateOptionsResult validation = new KafkaSettingsValidator().Validate(name: null, kafkaSettings);
            if (validation.Failed)
            {
                throw new InvalidOperationException(
                    $"Configuração Kafka inválida: {string.Join(" | ", validation.Failures ?? [])}");
            }

            KafkaTransportExpression kafka = opts.UseKafka(kafkaSettings.BootstrapServers);

            // ConfigureClient só quando há sobrescritas a aplicar — evita registrar callback
            // que mexe em propriedades default do Confluent.Kafka.ClientConfig.
            if (RequiresClientConfig(kafkaSettings))
            {
                kafka = kafka.ConfigureClient(config => KafkaSecurity.Apply(config, kafkaSettings));
            }

            kafka.AutoProvision();

            configureRouting?.Invoke(opts);
        });
    }

    internal static bool RequiresClientConfig(KafkaSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.SecurityProtocol)
        || !string.IsNullOrWhiteSpace(settings.SaslMechanism)
        || !string.IsNullOrWhiteSpace(settings.SaslUsername)
        || !string.IsNullOrWhiteSpace(settings.SaslPassword)
        || !string.IsNullOrWhiteSpace(settings.SslCaLocation)
        || !string.IsNullOrWhiteSpace(settings.SslCaPem);
}
