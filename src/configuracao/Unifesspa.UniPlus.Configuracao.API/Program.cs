using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Infrastructure.Core.Smoke;
using Unifesspa.UniPlus.Configuracao.API.Errors;
using Unifesspa.UniPlus.Configuracao.Application;
using Unifesspa.UniPlus.Configuracao.Infrastructure;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// service.name canônico — ver Selecao.API/Program.cs. Single source of truth
// em UniPlusServiceNames evita drift entre logs (Serilog/Loki) e traces (Tempo).
const string nomeServico = UniPlusServiceNames.Configuracao;

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration, nomeServico));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Minimal API endpoints use a separate JSON options pipeline.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
// OpenAPI 3.1 (ADR-0030) — spec em /openapi/configuracao.json.
builder.Services.AddUniPlusOpenApi("configuracao", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// Idempotency-Key (ADR-0027) + criptografia para entries at-rest. Filter global
// se ativa apenas em endpoints com [RequiresIdempotencyKey] — controllers admin
// entram em F2.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddIdempotency<ConfiguracaoDbContext>(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — tracing + metrics via OpenTelemetry SDK.
builder.Services.AdicionarObservabilidade(nomeServico, builder.Configuration, builder.Environment);

builder.Services.AddConfiguracaoApplication();
builder.Services.AddConfiguracaoInfrastructure();

// Migrations EF Core aplicadas no host StartAsync via IHostedService (issue #344).
// Registrado ANTES de UseWolverineOutboxCascading + AddWolverineMessaging (invariante
// #419) — fitness test em ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests
// cobre os 5 entry points com a regra.
builder.Services.AddDbContextMigrationsOnStartup<ConfiguracaoDbContext>();

// Wolverine como backbone CQRS/messaging (ADR-0003/0004/0005). Sem roteamento
// específico em V1 — eventos de catálogo (Modalidade*Event, etc.) entram em F2
// para invalidar cache cross-pod do reader (PG queue intra-módulo per ADR-0044).
builder.Host.UseWolverineOutboxCascading(builder.Configuration, connectionStringName: "ConfiguracaoDb");
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks: Postgres + Redis + MinIO + Kafka + OIDC.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "ConfiguracaoDb");

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
