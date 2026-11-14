namespace Unifesspa.UniPlus.Selecao.API;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.Kafka;
using Wolverine.Kafka.Serialization;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using ProcessoPublicadoAvro = unifesspa.uniplus.selecao.events.ProcessoPublicado;

/// <summary>
/// Wiring de mensageria do módulo Seleção (Kafka/Schema Registry — ADR-0051 — e
/// routing Wolverine — ADR-0003/0004/0005). É setup de processo (uma instância
/// Wolverine por host), então fica fora de <see cref="SelecaoModuleRegistration"/>:
/// tanto o Program.cs standalone quanto o composition root do monólito modular
/// chamam este mesmo método, garantindo paridade de comportamento.
/// </summary>
/// <remarks>
/// Slice canônico do ADR-0005/ADR-0041 (Story #759, T4 #785) — reconstrói sobre
/// <see cref="ProcessoPublicadoEvent"/> a demonstração que antes vivia sobre o
/// agregado <c>Edital</c> legado (removido por #782).
/// </remarks>
public static partial class SelecaoMessagingRegistration
{
    /// <summary>
    /// Registra o Schema Registry do módulo (cliente + schema Avro
    /// <c>processo_seletivo_events-value</c>) e devolve o configurador de routing
    /// Wolverine do Seleção, para o host compor no <c>configureRouting</c> de
    /// <c>UseWolverineOutboxCascading</c> — sem o host precisar conhecer Kafka/SR.
    /// </summary>
    /// <param name="relogio">
    /// Relógio do processo, o mesmo registrado no container: o bootstrap do cliente roda antes do <c>Build</c> e não
    /// o resolve pelo DI.
    /// </param>
    /// <returns>
    /// Ação que aplica o routing do Seleção a um <see cref="WolverineOptions"/>:
    /// PG queue intra-módulo <c>domain-events</c> + (quando Kafka e SR estão
    /// configurados) tópico Kafka <c>processo_seletivo_events</c> com serializer
    /// Avro Confluent.
    /// </returns>
    public static Action<WolverineOptions> AddSelecaoMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(relogio);

        ISchemaRegistryClient? srClient = RegistrarSchemaRegistry(services, configuration, environment, relogio);

        services.AddSchemaRegistry(configuration)
            .AddSchema(
                subject: "processo_seletivo_events-value",
                schemaResourceName: ProcessoPublicadoAvro.SchemaResourceName,
                resourceAssembly: typeof(ProcessoPublicadoEvent).Assembly);

        return opts => ConfigurarRouting(opts, configuration, srClient);
    }

    /// <summary>
    /// Routing do Seleção: drena <see cref="ProcessoPublicadoEvent"/> pela PG queue
    /// <c>domain-events</c> (cascading ADR-0005) e, quando Kafka + Schema Registry
    /// estão configurados, projeta o evento Avro para o tópico
    /// <c>processo_seletivo_events</c>.
    /// </summary>
    public static void ConfigurarRouting(
        WolverineOptions opts,
        IConfiguration configuration,
        ISchemaRegistryClient? srClient)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(configuration);

        ConfigurarFilaDosEventosDoProcesso(opts);
        ConfigurarFilaDaCopiaAoAcervo(opts);
        ConfigurarVerificacaoDosArquivos(opts, configuration);
        ConfigurarPublicacaoAvroNoKafka(opts, configuration, srClient);
    }

    private static void ConfigurarFilaDosEventosDoProcesso(WolverineOptions opts)
    {
        opts.PublishMessage<ProcessoPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");
    }

    /// <summary>
    /// A cópia dos documentos de um ato ao acervo público é o efeito posterior da publicação: sai pelo outbox, na
    /// transação que gravou a versão, o ato e a divulgação. Fila PostgreSQL, e não local: a local é em memória, e um
    /// encerramento entre o commit e a cópia deixaria o documento sem endereço para sempre. Sem regra de falha, a
    /// primeira exceção do consumo manda o envelope para a fila morta (ver <c>ReentregaDaCopiaAoAcervo</c>).
    /// </summary>
    private static void ConfigurarFilaDaCopiaAoAcervo(WolverineOptions opts)
    {
        opts.PublishMessage<CopiarDocumentosDoAtoAoAcervo>().ToPostgresqlQueue("selecao-copia-ao-acervo");
        opts.ListenToPostgresqlQueue("selecao-copia-ao-acervo");
        opts.Policies.Add<ReentregaDaCopiaAoAcervo>();
    }

    /// <summary>
    /// Com Kafka e Schema Registry configurados, produz Avro com o schema-id no envelope (wire format Confluent): os
    /// consumidores cross-módulo recuperam o schema pelo id, sem depender do assembly Selecao.Domain.
    /// </summary>
    private static void ConfigurarPublicacaoAvroNoKafka(
        WolverineOptions opts, IConfiguration configuration, ISchemaRegistryClient? srClient)
    {
        if (KafkaHabilitado(configuration) && srClient is not null)
        {
            opts.PublishMessage<ProcessoPublicadoAvro>()
                .ToKafkaTopic("processo_seletivo_events")
                .DefaultSerializer(new SchemaRegistryAvroSerializer(srClient));
        }
    }

    private static bool KafkaHabilitado(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Kafka:BootstrapServers"]);

    /// <summary>
    /// Transporte da verificação dos arquivos do candidato (ADR-0146): o pedido sai pelo outbox durável para a quorum
    /// queue de pedidos e o resultado chega pela de resultados. <c>RabbitMq:Uri</c> é obrigatória: sem ela o arquivo
    /// ficaria em verificação para sempre. O handler do resultado é preso à fila: o resultado do documento de
    /// designação, que chega pela fila da Autorização, não pode executar o da Seleção, e o contrário, porque os dois
    /// tratam o mesmo tipo de mensagem.
    /// </summary>
    internal static void ConfigurarVerificacaoDosArquivos(WolverineOptions opts, IConfiguration configuration)
    {
        opts.UseRabbitMq(UriDoRabbitMq(configuration)).AutoProvision();

        opts.PublishMessage<PedidoDeVerificacaoDeArquivo>()
            .ToRabbitQueue(FilasDaVerificacao.Pedidos, fila => fila.QueueType = QueueType.quorum);
        opts.ListenToRabbitQueue(FilasDaVerificacao.Resultados, fila => fila.QueueType = QueueType.quorum)
            .AddStickyHandler(typeof(RegistrarResultadoDaVerificacaoDoArquivoHandler));

        opts.Policies.Add<ReentregaDoResultadoDaVerificacao>();
    }

    private static Uri UriDoRabbitMq(IConfiguration configuration)
    {
        string? uri = configuration["RabbitMq:Uri"];

        return string.IsNullOrWhiteSpace(uri)
            ? throw new InvalidOperationException(
                "Configuração inválida: RabbitMq:Uri é obrigatório — é por ele que a API publica o pedido de verificação dos "
                + "arquivos do candidato e recebe o resultado (ADR-0146).")
            : new Uri(uri, UriKind.Absolute);
    }

    /// <summary>
    /// Cria o <see cref="ISchemaRegistryClient"/> a partir de <c>SchemaRegistry:Url</c> (ou <see langword="null"/>
    /// quando vazio) e o registra como singleton, aplicando a invariante ADR-0051/0053 (Kafka em ambiente produtivo
    /// exige Schema Registry). A posse do cliente fica com o dono registrado por <c>CreateClient</c>, que o host
    /// descarta no encerramento.
    /// </summary>
    private static ISchemaRegistryClient? RegistrarSchemaRegistry(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        TimeProvider relogio)
    {
        SchemaRegistrySettings srSettings = configuration
            .GetSection(SchemaRegistrySettings.SectionName)
            .Get<SchemaRegistrySettings>() ?? new SchemaRegistrySettings();

        ConferirSchemaRegistryDoKafka(configuration, environment, srSettings);

        return string.IsNullOrWhiteSpace(srSettings.Url) ? null : RegistrarClienteDoBootstrap(services, srSettings, relogio);
    }

    private static ISchemaRegistryClient RegistrarClienteDoBootstrap(
        IServiceCollection services, SchemaRegistrySettings srSettings, TimeProvider relogio)
    {
        using ILoggerFactory bootstrapLoggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
        ISchemaRegistryClient srClient = SchemaRegistryServiceCollectionExtensions.CreateClient(
            srSettings,
            bootstrapLoggerFactory,
            services,
            relogio);
        services.AddSingleton(srClient);

        return srClient;
    }

    /// <summary>
    /// Kafka habilitado exige Schema Registry em ambientes produtivos (ADR-0051/0053); só Development afrouxa, o que
    /// cobre as test factories que sobem com <c>UseEnvironment("Development")</c>.
    /// </summary>
    private static void ConferirSchemaRegistryDoKafka(
        IConfiguration configuration, IHostEnvironment environment, SchemaRegistrySettings srSettings)
    {
        if (!KafkaHabilitado(configuration) || !string.IsNullOrWhiteSpace(srSettings.Url))
        {
            return;
        }

        RecusarKafkaSemSchemaRegistryForaDeDevelopment(environment);
        AvisarKafkaSemSchemaRegistry(environment.EnvironmentName);
    }

    private static void RecusarKafkaSemSchemaRegistryForaDeDevelopment(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Configuração inválida: Kafka:BootstrapServers populado mas SchemaRegistry:Url vazio em "
                + $"ASPNETCORE_ENVIRONMENT={environment.EnvironmentName}. "
                + "ADR-0051 exige Schema Registry para todo publishing cross-módulo em ambientes produtivos. "
                + "Configure SchemaRegistry:Url (ou desligue Kafka apagando Kafka:BootstrapServers).");
        }
    }

    private static void AvisarKafkaSemSchemaRegistry(string ambiente)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
        ILogger logger = loggerFactory.CreateLogger("Selecao.Messaging.Bootstrap");
        LogKafkaSemSchemaRegistry(logger, ambiente);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Kafka habilitado sem Schema Registry em ambiente {Env} — publishing Avro cross-módulo desligado. Esperado em test factory que isola cascading puro; sinal de bug em ambiente produtivo (ADR-0051).")]
    private static partial void LogKafkaSemSchemaRegistry(ILogger logger, string env);
}
