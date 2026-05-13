using Microsoft.Extensions.DependencyInjection.Extensions;

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
using Unifesspa.UniPlus.Portal.API.Errors;
using Unifesspa.UniPlus.Portal.Infrastructure;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// service.name canônico — ver explicação em Selecao.API/Program.cs. Single
// source of truth em UniPlusServiceNames evita drift entre logs e traces.
const string nomeServicoPortal = UniPlusServiceNames.Portal;

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration, nomeServicoPortal));

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

// TimeProvider — registrado explicitamente porque Selecao/Ingresso o ganham
// indiretamente via AddCursorPagination/AddIdempotency, mas o Portal (esqueleto)
// não invoca nenhum dos dois. Sem este TryAdd, PingController e o handler de
// /api/profile/me (TimeProvider clock) falham na ativação. TryAddSingleton
// preserva overrides de teste (ex.: FakeTimeProvider) — alinhado com o
// padrão usado em CursorPaginationServiceCollectionExtensions e
// IdempotencyServiceCollectionExtensions.
builder.Services.TryAddSingleton(TimeProvider.System);

builder.Services.AddEndpointsApiExplorer();
// OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo + transformers Uni+
// (info, operation, schema). Spec exposto em /openapi/portal.json.
builder.Services.AddUniPlusOpenApi("portal", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, PortalDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — ver explicação em Selecao.API/Program.cs.
builder.Services.AdicionarObservabilidade(nomeServicoPortal, builder.Configuration, builder.Environment);

// AddPortalInfrastructure resolve a connection string via IConfiguration
// injetada no factory do AddDbContext — simetria com Selecao/Ingresso (#204).
builder.Services.AddPortalInfrastructure();

// Migrations EF Core do módulo Portal aplicadas no host StartAsync via IHostedService —
// pareado com AutoBuildMessageStorageOnStartup do Wolverine (issue #344). Como hosted
// service, o registro é filtrável por test factories que sobem o pipeline HTTP sem
// Postgres real (ver ApiFactoryBase). Idempotente; banco já migrado é no-op.
//
// INVARIANTE (#419): registrado antes de UseWolverineOutboxCascading +
// AddWolverineMessaging — mesma justificativa do Selecao.API. Fitness em
// tests/Unifesspa.UniPlus.ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests
// cobre os 3 entry points.
builder.Services.AddDbContextMigrationsOnStartup<PortalDbContext>();

// Wolverine como backbone CQRS/messaging com outbox transacional —
// ver ADR-0003, ADR-0004 e ADR-0005. Esqueleto sem rotas adicionais
// (não há domain events publicáveis ainda); a Story que introduzir o
// primeiro caso de uso completa o roteamento.
builder.Host.UseWolverineOutboxCascading(builder.Configuration, connectionStringName: "PortalDb");
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks agregados: Postgres + Redis + MinIO + Kafka + OIDC (já registrado por
// AddOidcAuthentication acima). Endpoints separados em /health/live (deps-free), /health/ready
// (todos com tag "ready") e /health (alias retrocompat).
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "PortalDb");

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
// Readiness: agrega checks tagueados "ready" (Postgres, Redis, MinIO, Kafka, OIDC).
// Reflete o estado real das deps externas — Kubernetes deve apontar readinessProbe aqui.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = h => h.Tags.Contains(HealthChecksServiceCollectionExtensions.ReadyTag),
});
// Alias retrocompat: continua expondo /health (sem filtro = todos os checks). Pode ser removido
// quando todos os clients consumirem /health/live ou /health/ready.
app.MapHealthChecks("/health");

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests; CA1515
// suppression lives in GlobalSuppressions.cs, not inline.
public partial class Program;
