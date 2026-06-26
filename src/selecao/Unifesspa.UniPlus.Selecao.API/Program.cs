using Serilog;

using Confluent.SchemaRegistry;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Infrastructure.Core.Smoke;
using Unifesspa.UniPlus.Selecao.API;
using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

using Wolverine.Kafka;
using Wolverine.Kafka.Serialization;
using Wolverine.Postgresql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// service.name canônico para Resource OTel (tracing/metrics), ResourceAttributes
// do sink OTLP do Serilog (logs) e propriedade Serilog `ServiceName` via
// ServiceNameEnricher (ADR-0052). Single source of truth em UniPlusServiceNames —
// drift de naming entre logs e traces seria a 1ª coisa a quebrar drill-down
// Loki↔Tempo no Grafana.
const string nomeServicoSelecao = UniPlusServiceNames.Selecao;

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration, nomeServicoSelecao));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Enums em wire format como nome camelCase (ex.: "etapa", não 1).
        // Alinha com o Newman seed (ADR-0062) e admin DX em pt-BR. Sem isso,
        // qualquer payload admin com enum como string vira 400 — quebra
        // CategoriaObrigatoriedade no #461, futuras enums em #455 etc.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// Minimal API endpoints use a separate JSON options pipeline (ConfigureHttpJsonOptions),
// not the MVC one above. Keep both in sync for consistent serialization across the API.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services.AddEndpointsApiExplorer();
// AddDomainErrorMapper é compartilhado (consome IEnumerable<IDomainErrorRegistration>);
// o registro de erros do módulo entra via AddSelecaoModule.
builder.Services.AddDomainErrorMapper();

// Criptografia + cursor pagination usados pelos endpoints de listagem
// (ADR-0026 + ADR-0031). AddUniPlusEncryption: provider 'local' AES-GCM 256
// em dev/CI; troca para 'vault' em produção via configuração, sem code change.
// AddCursorPagination: registra CursorEncoder, TimeProvider.System,
// CursorPaginationOptions e o hook InvalidModelStateResponseFactory que
// traduz falhas do PageRequestModelBinder para 400/410/422.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — tracing + metrics via OpenTelemetry SDK para o Collector
// institucional. Logs já fluem via Serilog OTLP sink configurado em UseSerilog acima.
// Toggle Observability:Enabled em appsettings; default true. Em test factories sem
// Collector provisionado, sobrescrever para false em InMemoryCollection.
builder.Services.AdicionarObservabilidade(nomeServicoSelecao, builder.Configuration, builder.Environment);

// Registro self-describing do módulo: OpenAPI, erros de domínio, HATEOAS,
// idempotência, Application + Infrastructure e migrations on startup. O mesmo
// método é consumido pelo composition root do monólito modular (spike).
// Migrations on startup ficam ANTES do Wolverine (invariante #419).
builder.Services.AddSelecaoModule(builder.Configuration);

// Schema Registry (ADR-0051) — feature off quando SchemaRegistry:Url está vazio.
// Cliente único reutilizado pelo hosted service (registro idempotente no startup)
// e pelo Wolverine routing (SchemaRegistryAvroSerializer no producer Kafka).
// Singleton registrado ANTES do AddSchemaRegistry para que o TryAddSingleton
// interno respeite a instância pré-criada e não duplique cache.
SchemaRegistrySettings selecaoSrSettings = builder.Configuration
    .GetSection(SchemaRegistrySettings.SectionName)
    .Get<SchemaRegistrySettings>() ?? new SchemaRegistrySettings();

// Invariante operacional: Kafka habilitado exige Schema Registry configurado.
// ADR-0051 estabelece que mensagens em tópicos cross-módulo do Uni+ vão sempre
// como Avro com schema-id no envelope. Sem SR, o publishing seria silenciosamente
// desligado — consumers cross-módulo parariam de receber sem qualquer erro no
// boot. Falha imediata orientando o operador é o correto em ambientes produtivos.
//
// Test factories (CascadingFixture e similares) sobem Wolverine com Kafka real
// mas sem SR para testar cascading puro — em ASPNETCORE_ENVIRONMENT=Test/Development
// degradamos para warning ao invés de exceção, mantendo o fail-fast para
// Production/Staging/Standalone onde a config real do operador rege.
bool kafkaEnabledForBuilder = !string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]);
bool srMissing = string.IsNullOrWhiteSpace(selecaoSrSettings.Url);
if (kafkaEnabledForBuilder && srMissing)
{
    // ADR-0053: HML/sanidade/Prod = mesmo binário, Vault injeta config — só
    // Development (= local dev box) afrouxa a obrigatoriedade do Schema Registry.
    // Test factories sobem com `UseEnvironment("Development")` (ApiFactoryBase),
    // o que cobre o cenário antes resolvido via comparação literal `"Test"`.
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Configuração inválida: Kafka:BootstrapServers populado mas SchemaRegistry:Url vazio em "
            + $"ASPNETCORE_ENVIRONMENT={builder.Environment.EnvironmentName}. "
            + "ADR-0051 exige Schema Registry para todo publishing cross-módulo em ambientes produtivos. "
            + "Configure SchemaRegistry:Url (ou desligue Kafka apagando Kafka:BootstrapServers).");
    }

    using ILoggerFactory missingSrLoggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
    Microsoft.Extensions.Logging.ILogger missingSrLogger = missingSrLoggerFactory.CreateLogger("Selecao.API.Bootstrap");
#pragma warning disable CA1848 // Bootstrap logging — fora do hot path; LoggerMessage source generator overkill aqui.
#pragma warning disable CA2254 // Mensagem fixa após format de string interpolada — sem placeholders dinâmicos.
    missingSrLogger.LogWarning(
        "Kafka habilitado sem Schema Registry em ambiente {Env} — publishing Avro cross-módulo desligado. "
        + "Esperado em test factory que isola cascading puro; sinal de bug em ambiente produtivo (ADR-0051).",
        builder.Environment.EnvironmentName);
#pragma warning restore CA2254
#pragma warning restore CA1848
}

ISchemaRegistryClient? selecaoSrClient = null;
if (!string.IsNullOrWhiteSpace(selecaoSrSettings.Url))
{
    using ILoggerFactory bootstrapLoggerFactory = LoggerFactory.Create(static b => b.AddSerilog());
    // CA2000 supprimido: o cliente retornado é registrado como singleton no DI
    // logo abaixo (AddSingleton) — Microsoft.Extensions.DependencyInjection
    // assume ownership e dispõe no shutdown do IHost. Top-level statements em
    // Program.cs não suportam [SuppressMessage] por símbolo, daí pragma inline
    // (alinhado com pattern já usado no AwaitWolverineDuringDispose etc.).
    // roslyn-analyzers#5447 — analisador não rastreia ownership via DI.
#pragma warning disable CA2000
    selecaoSrClient = SchemaRegistryServiceCollectionExtensions.CreateClient(
        selecaoSrSettings,
        bootstrapLoggerFactory,
        builder.Services);
#pragma warning restore CA2000
    builder.Services.AddSingleton(selecaoSrClient);
}

builder.Services.AddSchemaRegistry(builder.Configuration)
    .AddSchema(
        subject: "edital_events-value",
        schemaResourceName: unifesspa.uniplus.selecao.events.EditalPublicado.SchemaResourceName,
        resourceAssembly: typeof(EditalPublicadoEvent).Assembly);

// INVARIANTE (#419): AddSelecaoModule (acima) registra AddDbContextMigrationsOnStartup
// ANTES deste UseWolverineOutboxCascading + AddWolverineMessaging.
// HostOptions.ServicesStartConcurrently=false (default) garante que IHostedService
// inicia sequencialmente na ordem de registro — então MigrationHostedService aplica o
// schema EF do domínio ANTES do WolverineRuntime aceitar o primeiro envelope cascading,
// evitando 42P01 em handlers que tocam tabelas do módulo. Fitness test em
// tests/Unifesspa.UniPlus.ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests
// trava regressão de ordem nos 3 entry points (Selecao/Ingresso/Portal).
//
// Wolverine como backbone CQRS/messaging com outbox transacional —
// ver ADR-0003, ADR-0004 e ADR-0005.
//
// Configuração (ADR-0004 + ADR-0005):
//   - PersistMessagesWithPostgresql: outbox durável no schema "wolverine"
//     do mesmo banco do módulo (SelecaoDb).
//   - UseEntityFrameworkCoreTransactions + AutoApplyTransactions:
//     atomicidade write+evento — envelope persistido na MESMA transação
//     do SaveChanges via IEnvelopeTransaction.
//   - UseDurableOutboxOnAllSendingEndpoints: rota durável é invariante.
//   - PublishDomainEventsFromEntityFrameworkCore NÃO é chamado (ADR-0005):
//     drenagem de EntityBase.DomainEvents via cascading messages no
//     retorno do handler (IEnumerable<object>), não pelo scraper EF.
//   - Routing de EditalPublicadoEvent: PG queue "domain-events" intra-módulo;
//     o cascading handler EditalPublicadoToKafkaCascadeHandler (Selecao.Infrastructure)
//     projeta para EditalPublicadoAvro, que é roteado ao tópico Kafka
//     "edital_events" com Confluent SR Avro serializer (ADR-0051).
//
// A leitura de connection string e Kafka bootstrap acontece dentro do
// callback de UseWolverine no startup do host. Em testes integrados,
// os overrides chegam via env vars (ConnectionStrings__SelecaoDb,
// Kafka__BootstrapServers) — overrides via ConfigureAppConfiguration
// não propagam para WebApplicationBuilder.Configuration em minimal API.
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "SelecaoDb",
    configureRouting: opts =>
    {
        // Wolverine escaneia o entry assembly (Selecao.API) por padrão; handlers
        // produtivos vivem em Selecao.Application e Selecao.Infrastructure —
        // incluir explicitamente para que PublicarEditalCommandHandler e o
        // cascading EditalPublicadoToKafkaCascadeHandler sejam descobertos.
        opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly);
        opts.Discovery.IncludeAssembly(typeof(EditalPublicadoToKafkaCascadeHandler).Assembly);

        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        bool kafkaConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]);
        if (kafkaConfigured && selecaoSrClient is not null)
        {
            // Kafka habilitado + Schema Registry disponível: produz Avro com schema-id
            // no envelope (Confluent wire format). Consumers cross-módulo recuperam o
            // schema do Apicurio via schema-id, sem dependência do assembly Selecao.Domain.
            opts.PublishMessage<unifesspa.uniplus.selecao.events.EditalPublicado>()
                .ToKafkaTopic("edital_events")
                .DefaultSerializer(new SchemaRegistryAvroSerializer(selecaoSrClient));
        }
    });
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks agregados: Postgres + Redis + MinIO + Kafka + OIDC. Ver Portal.API/Program.cs
// para a explicação completa do split /health/live vs /health/ready.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "SelecaoDb");

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCorsConfiguration();
app.UseAuthentication();
app.UseAuthorization();
app.MapSharedAuthEndpoints();
app.MapSharedProfileEndpoints();
app.MapUniPlusSmokeEndpoints();
app.MapControllers();
app.MapOpenApi("/openapi/{documentName}.json");
// Liveness dependency-free: 200 enquanto o processo está respondendo, sem
// avaliar checks externos (OIDC, Postgres, Kafka, Redis). Predicate => false
// resulta em healthy quando nenhum check passa pelo filtro — exatamente o
// comportamento que queremos para evitar restart loops do Kubernetes
// quando uma dependência transient cai. Readiness mantém o /health agregado.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = h => h.Tags.Contains(HealthChecksServiceCollectionExtensions.ReadyTag),
});
app.MapHealthChecks("/health");

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests; CA1515
// suppression lives in GlobalSuppressions.cs, not inline.
public partial class Program;
