using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Ingresso.API.Errors;
using Unifesspa.UniPlus.Ingresso.Infrastructure;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

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
// (info, operation, schema). Spec exposto em /openapi/ingresso.json.
builder.Services.AddUniPlusOpenApi("ingresso", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, IngressoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);
// AddIngressoInfrastructure agora resolve a connection string via
// IConfiguration injetada no factory do AddDbContext (issue #204) —
// simetria com UseWolverineOutboxCascading e com Selecao.
builder.Services.AddIngressoInfrastructure();

// Wolverine como backbone CQRS/messaging com outbox transacional —
// ver ADR-0003, ADR-0004 e ADR-0005.
//
// Mesma forma da Selecao.API (ver Selecao.API/Program.cs): outbox
// durável Postgres + cascading + Kafka opcional. Sem roteamento
// específico até CandidatoConvocadoEvent/MatriculaEfetivadaEvent
// ganharem destino cross-módulo.
builder.Host.UseWolverineOutboxCascading(builder.Configuration, connectionStringName: "IngressoDb");
builder.Services.AddWolverineMessaging();

// Migrations EF Core do módulo Ingresso aplicadas no host StartAsync via IHostedService
// (issue #344). Como hosted service, o registro é filtrável por test factories que sobem
// o pipeline HTTP sem Postgres real (ver ApiFactoryBase). Idempotente.
builder.Services.AddDbContextMigrationsOnStartup<IngressoDbContext>();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks agregados: Postgres + Redis + MinIO + Kafka + OIDC. Ver Portal.API/Program.cs
// para a explicação completa do split /health/live vs /health/ready.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "IngressoDb");

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
