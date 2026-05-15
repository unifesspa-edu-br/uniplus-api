using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Infrastructure.Core.Smoke;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// service.name canônico — ver explicação em Selecao.API/Program.cs. Single
// source of truth em UniPlusServiceNames evita drift entre logs e traces.
const string nomeServico = UniPlusServiceNames.OrganizacaoInstitucional;

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
// OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo. Spec em /openapi/organizacao.json.
builder.Services.AddUniPlusOpenApi("organizacao", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// HATEOAS Level 1 (ADR-0029/0049) — builder de _links para AreaOrganizacionalDto.
builder.Services.AddSingleton<IResourceLinksBuilder<AreaOrganizacionalDto>, AreaOrganizacionalLinksBuilder>();

// Criptografia (Idempotency response cipher) + Idempotency-Key (ADR-0027).
// AddIdempotency injeta o filter de MVC que ativa-se em endpoints com
// [RequiresIdempotencyKey], usando o DbContext do módulo para persistir
// entries cifradas at-rest.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddIdempotency<OrganizacaoInstitucionalDbContext>(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — tracing + metrics via OpenTelemetry SDK.
builder.Services.AdicionarObservabilidade(nomeServico, builder.Configuration, builder.Environment);

// Application + Infrastructure — registram validators, DbContext, repositórios,
// reader cross-módulo (IAreaOrganizacionalReader) e cache invalidator.
builder.Services.AddOrganizacaoInstitucionalApplication();
builder.Services.AddOrganizacaoInstitucionalInfrastructure();

// Migrations EF Core aplicadas no host StartAsync via IHostedService (issue #344).
// Registrado ANTES de UseWolverineOutboxCascading + AddWolverineMessaging (invariante
// #419) — HostOptions.ServicesStartConcurrently=false (default) garante inicialização
// sequencial, então o schema EF é aplicado antes do runtime Wolverine aceitar envelopes.
// Filtrável por test factories HTTP-only (ApiFactoryBase).
builder.Services.AddDbContextMigrationsOnStartup<OrganizacaoInstitucionalDbContext>();

// Wolverine como backbone CQRS/messaging (ADR-0003/0004/0005). Sem roteamento
// específico — o módulo não emite eventos Kafka em V1 (D4 do plano #447); o
// wire-up básico permanece para que ICommandBus/IQueryBus funcionem com
// middleware de validação e logging.
builder.Host.UseWolverineOutboxCascading(builder.Configuration, connectionStringName: "OrganizacaoDb");
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks agregados: Postgres + Redis + MinIO + Kafka + OIDC. Mesmo split
// /health/live (dependency-free) vs /health/ready (full) dos outros módulos.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "OrganizacaoDb");

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
