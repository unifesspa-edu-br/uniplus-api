namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Middleware;

using Unifesspa.UniPlus.Kernel.Tempo;

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
        RecusarFormaLegadaDaSecaoDoKafka(kafkaConfigSection);

        host.ConfigureServices(services => RegistrarOpcoesDoKafka(services, configuration, kafkaConfigSection));

        return host.UseWolverine(
            opts => ConfigurarWolverine(opts, configuration, connectionStringName, kafkaConfigSection, configureRouting));
    }

    internal static bool RequiresClientConfig(KafkaSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.SecurityProtocol)
        || !string.IsNullOrWhiteSpace(settings.SaslMechanism)
        || !string.IsNullOrWhiteSpace(settings.SaslUsername)
        || !string.IsNullOrWhiteSpace(settings.SaslPassword)
        || !string.IsNullOrWhiteSpace(settings.SslCaLocation)
        || !string.IsNullOrWhiteSpace(settings.SslCaPem);

    private static void ConfigurarWolverine(
        WolverineOptions opts,
        IConfiguration configuration,
        string connectionStringName,
        string kafkaConfigSection,
        Action<WolverineOptions>? configureRouting)
    {
        ConfigurarSerializacaoDasMensagens(opts);
        ConfigurarOutboxTransacional(opts, ConnectionStringDoOutbox(configuration, connectionStringName));
        ConfigurarGeracaoDeCodigoEDescoberta(opts);
        ConfigurarMiddlewaresDoPipeline(opts);
        ConfigurarProvisionamentoDoSchema(opts, configuration);
        ConfigurarTransporteKafka(opts, LerOpcoesDoKafka(configuration, kafkaConfigSection));
        configureRouting?.Invoke(opts);
    }

    /// <summary>
    /// Recusa a forma legada exata deste parâmetro: até o #343 era a chave inteira do bootstrap
    /// (<c>Kafka:BootstrapServers</c>). Ligar essa chave terminal como se fosse o nome de uma seção devolveria um
    /// <see cref="KafkaSettings"/> vazio e desligaria o transporte sem aviso. A recusa vale só para o sufixo
    /// <c>:BootstrapServers</c>, preservando as seções aninhadas legítimas (<c>Messaging:Kafka</c>).
    /// </summary>
    private static void RecusarFormaLegadaDaSecaoDoKafka(string kafkaConfigSection)
    {
        if (kafkaConfigSection.EndsWith(":BootstrapServers", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"kafkaConfigSection '{kafkaConfigSection}' parece a forma legada do parâmetro (chave terminal `Kafka:BootstrapServers`). "
                + "Passe o nome da seção (ex.: 'Kafka' ou 'Messaging:Kafka').",
                nameof(kafkaConfigSection));
        }
    }

    /// <summary>
    /// Liga e valida <see cref="KafkaSettings"/> pelo DI. O <c>ValidateOnStart</c> roda no start do host, antes de
    /// qualquer hosted service consumir o Kafka, e reporta no formato canônico do .NET
    /// (<see cref="OptionsValidationException"/> com a lista de campos faltantes).
    /// </summary>
    private static void RegistrarOpcoesDoKafka(IServiceCollection services, IConfiguration configuration, string kafkaConfigSection)
    {
        services.AddOptions<KafkaSettings>()
            .Bind(configuration.GetSection(kafkaConfigSection))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<KafkaSettings>, KafkaSettingsValidator>();
    }

    private static string ConnectionStringDoOutbox(IConfiguration configuration, string connectionStringName)
    {
        string? connectionString = configuration.GetConnectionString(connectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' não configurada — Wolverine não pode inicializar o outbox.")
            : connectionString;
    }

    /// <summary>
    /// Serializador JSON padrão das mensagens, o mesmo contrato de instantes que o verificador aplica do outro lado do
    /// RabbitMQ (<see cref="OpcoesJsonDasMensagens"/>). Vem antes de qualquer endpoint: os que não declaram serializador
    /// próprio, como as filas PostgreSQL, usam o padrão. O Kafka declara o Avro por endpoint e não passa por aqui.
    /// </summary>
    private static void ConfigurarSerializacaoDasMensagens(WolverineOptions opts) =>
        opts.UseSystemTextJsonForSerialization(OpcoesJsonDasMensagens.Aplicar);

    /// <summary>
    /// Persiste o outbox no mesmo banco do módulo, em schema isolado; <c>EnableMessageTransport(_ => { })</c> ativa o
    /// transporte interno da fila PostgreSQL. A atomicidade write+evento vem de o handler que muta agregado e retorna
    /// cascading messages ter o envelope persistido na MESMA transação do <c>SaveChanges</c>, pelo
    /// <c>IEnvelopeTransaction</c> instalado por <c>EnrollDbContextInTransaction</c>.
    /// </summary>
    private static void ConfigurarOutboxTransacional(WolverineOptions opts, string connectionString)
    {
        opts.PersistMessagesWithPostgresql(connectionString, PersistenceSchema)
            .EnableMessageTransport(_ => { });

        opts.UseEntityFrameworkCoreTransactions();
        opts.Policies.AutoApplyTransactions();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    }

    /// <summary>
    /// <c>NotAllowed</c> adota cedo o default do Wolverine 6.0 (ADR-0098): a chain de handler cuja árvore de
    /// dependências contém uma lambda factory opaca, ou um tipo concreto não público, FALHA na geração de código
    /// (<c>InvalidServiceLocationException</c>) em vez de cair em service location em runtime. A suíte de integração,
    /// que sobe e exercita as chains dos hosts, quebra se uma dependência opaca vazar para um handler. Os opt-ins por
    /// tipo (UoW que encaminham para a MESMA instância de DbContext, ADR-0004) são declarados por cada módulo via
    /// <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c> e compostos pelo host no <c>configureRouting</c>; este helper
    /// permanece agnóstico dos tipos de módulo. O assembly Infrastructure.Core entra na descoberta para que os handlers
    /// compartilhados sejam encontrados sem cada Program.cs repetir o <c>IncludeAssembly</c>; o assembly de entrada já
    /// é varrido por padrão, então o registro é defensivo.
    /// </summary>
    private static void ConfigurarGeracaoDeCodigoEDescoberta(WolverineOptions opts)
    {
        opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.NotAllowed;
        opts.Discovery.IncludeAssembly(typeof(WolverineOutboxConfiguration).Assembly);
    }

    /// <summary>
    /// O <c>CorrelationIdEnvelopeMiddleware</c> roda em TODOS os chains, inclusive event handlers consumidos do Kafka
    /// (ADR-0052), e entra ANTES do middleware CQRS para o escopo do <c>LogContext</c> já estar ativo quando o
    /// <c>WolverineLoggingMiddleware</c> emite a entrada inicial. O middleware CQRS (logging + validação
    /// FluentValidation) vale só para chains de <c>ICommand&lt;&gt;</c>/<c>IQuery&lt;&gt;</c>. A propagação do header
    /// <c>uniplus.correlation-id</c> do envelope incoming (HTTP, outbox ou Kafka) para todas as mensagens outgoing do
    /// mesmo handler — cascading, <c>IMessageBus.PublishAsync</c>, agendamentos — fecha o CorrelationId via Kafka; o
    /// middleware de correlação garante o header no incoming, gerando um GUID se ausente ou inválido.
    /// </summary>
    private static void ConfigurarMiddlewaresDoPipeline(WolverineOptions opts)
    {
        opts.AddCorrelationIdMiddleware();
        opts.AddCommandQueryMiddleware();
        opts.Policies.PropagateIncomingHeaderToOutgoing(CorrelationIdEnvelopeMiddleware.HeaderName);
    }

    /// <summary>
    /// O schema do Wolverine (<c>wolverine_outgoing_envelopes</c>, <c>wolverine_incoming_envelopes</c>,
    /// <c>wolverine_node_assignments</c> etc.) é criado e atualizado no startup: o Wolverine inspeciona o schema atual
    /// e aplica só o delta, e réplicas subindo juntas são coordenadas pelo lock interno do framework. Os ambientes do
    /// Uni+ não têm orquestração de schema externa ao host, e o host criar o schema destrava o bring-up de pods com
    /// banco vazio. A exceção é o modo <c>Skip</c> (ADR-0127): ali quem provisiona o schema, o das migrations EF e o do
    /// próprio Wolverine, é o Job de deploy, que roda antes do rollout. Manter <c>CreateOrUpdate</c> no pod aplicaria DDL
    /// no start mesmo assim, e uma falha nesse DDL voltaria a acontecer DEPOIS de o rollout ter começado.
    /// </summary>
    private static void ConfigurarProvisionamentoDoSchema(WolverineOptions opts, IConfiguration configuration)
    {
        opts.AutoBuildMessageStorageOnStartup =
            DependencyInjection.MigrationExecutionModeExtensions.ProvisionamentoDeSchemaPara(
                DependencyInjection.MigrationExecutionModeExtensions.LerModoDeMigration(configuration));
    }

    /// <summary>
    /// Lê a seção uma única vez, no callback: o <c>ValidateOnStart</c> do DI não atinge este ponto, porque o
    /// <c>UseWolverine</c> roda no Build, antes de <c>StartAsync</c>.
    /// </summary>
    private static KafkaSettings LerOpcoesDoKafka(IConfiguration configuration, string kafkaConfigSection) =>
        configuration.GetSection(kafkaConfigSection).Get<KafkaSettings>() ?? new KafkaSettings();

    /// <summary>
    /// Sem <c>BootstrapServers</c> o transporte Kafka não é registrado e só a fila PostgreSQL fica ativa. A validação
    /// inline replica as regras essenciais para falhar antes de o <c>AutoProvision</c> tentar conectar com
    /// configuração inválida; o <c>IValidateOptions</c> registrado no DI garante o mesmo erro padronizado a outro
    /// consumidor de <c>IOptions&lt;KafkaSettings&gt;</c>. O <c>ConfigureClient</c> só entra quando há sobrescritas a
    /// aplicar, para não registrar callback que mexe nas propriedades default do <c>ClientConfig</c>.
    /// </summary>
    private static void ConfigurarTransporteKafka(WolverineOptions opts, KafkaSettings kafkaSettings)
    {
        if (string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
        {
            return;
        }

        ExigirOpcoesDoKafkaValidas(kafkaSettings);
        KafkaTransportExpression kafka = opts.UseKafka(kafkaSettings.BootstrapServers);
        if (RequiresClientConfig(kafkaSettings))
        {
            kafka = kafka.ConfigureClient(config => KafkaSecurity.Apply(config, kafkaSettings));
        }

        kafka.AutoProvision();
    }

    private static void ExigirOpcoesDoKafkaValidas(KafkaSettings kafkaSettings)
    {
        ValidateOptionsResult validation = new KafkaSettingsValidator().Validate(name: null, kafkaSettings);
        if (validation.Failed)
        {
            throw new InvalidOperationException(
                $"Configuração Kafka inválida: {string.Join(" | ", validation.Failures ?? [])}");
        }
    }
}
