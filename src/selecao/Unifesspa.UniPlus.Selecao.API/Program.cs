using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.API.Hateoas;
using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine.Kafka;
using Wolverine.Postgresql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Minimal API endpoints use a separate JSON options pipeline (ConfigureHttpJsonOptions),
// not the MVC one above. Keep both in sync for consistent serialization across the API.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
// OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo + transformers Uni+
// (info, operation, schema). Spec exposto em /openapi/selecao.json.
builder.Services.AddUniPlusOpenApi("selecao", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// HATEOAS Level 1 (ADR-0029) — builder de _links por recurso. Singleton
// porque encapsula apenas um LinkGenerator (também singleton); função pura.
builder.Services.AddSingleton<IResourceLinksBuilder<Unifesspa.UniPlus.Selecao.Application.DTOs.EditalDto>, EditalLinksBuilder>();

// Criptografia + cursor pagination usados pelos endpoints de listagem
// (ADR-0026 + ADR-0031). AddUniPlusEncryption: provider 'local' AES-GCM 256
// em dev/CI; troca para 'vault' em produção via configuração, sem code change.
// AddCursorPagination: registra CursorEncoder, TimeProvider.System,
// CursorPaginationOptions e o hook InvalidModelStateResponseFactory que
// traduz falhas do PageRequestModelBinder para 400/410/422.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);
// Idempotency-Key (ADR-0027) — store EF adjacente ao SelecaoDbContext, filter
// global que se ativa apenas em endpoints com [RequiresIdempotencyKey].
builder.Services.AddIdempotency<Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.SelecaoDbContext>(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);
builder.Services.AddSelecaoApplication();
// AddSelecaoInfrastructure agora resolve a connection string via
// IConfiguration injetada no factory do AddDbContext (issue #204) —
// simetria com UseWolverineOutboxCascading que já fazia leitura lazy.
// Test hosts (CascadingApiFactory) podem sobrescrever via env var ou
// InMemoryCollection sem precisar re-registrar o DbContext.
builder.Services.AddSelecaoInfrastructure();

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
//   - Routing de EditalPublicadoEvent: PG queue "domain-events" +
//     tópico Kafka "edital_events" quando bootstrap configurado.
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
        // produtivos vivem em Selecao.Application — incluir explicitamente para
        // que PublicarEditalCommandHandler (e futuros) sejam descobertos.
        opts.Discovery.IncludeAssembly(typeof(PublicarEditalCommand).Assembly);

        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        if (!string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]))
        {
            opts.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");
        }
    });
builder.Services.AddWolverineMessaging();

// Migrations EF Core do módulo Selecao aplicadas no host StartAsync via IHostedService
// (issue #344). Como hosted service, o registro é filtrável por test factories que sobem
// o pipeline HTTP sem Postgres real (ver ApiFactoryBase). Idempotente.
builder.Services.AddDbContextMigrationsOnStartup<SelecaoDbContext>();

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
