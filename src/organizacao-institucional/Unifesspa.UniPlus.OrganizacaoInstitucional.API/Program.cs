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
using Unifesspa.UniPlus.OrganizacaoInstitucional.API;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application;

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
// AddDomainErrorMapper é compartilhado (consome IEnumerable<IDomainErrorRegistration>);
// o registro de erros do módulo entra via AddOrganizacaoInstitucionalModule.
builder.Services.AddDomainErrorMapper();

// Criptografia (entries de idempotência at-rest) e cursor pagination (ADR-0026)
// são transversais ao host. Cursor pagination registra CursorEncoder,
// PageRequestModelBinder e o hook que traduz falhas do binder para 400/410/422.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — tracing + metrics via OpenTelemetry SDK.
builder.Services.AdicionarObservabilidade(nomeServico, builder.Configuration, builder.Environment);

// Registro self-describing do módulo: OpenAPI, erros de domínio, HATEOAS,
// idempotência, Application + Infrastructure e migrations on startup. O mesmo
// método é consumido pelo composition root do monólito modular.
// Migrations on startup ficam ANTES do Wolverine (invariante #419).
builder.Services.AddOrganizacaoInstitucionalModule(builder.Configuration);

// Wolverine como backbone CQRS/messaging (ADR-0003/0004/0005). Sem roteamento
// específico — o módulo não emite eventos Kafka em V1 (D4 do plano #447); o
// wire-up básico permanece para que ICommandBus/IQueryBus funcionem com
// middleware de validação e logging.
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "OrganizacaoDb",
    configureRouting: opts =>
    {
        // Wolverine escaneia o entry assembly (OrganizacaoInstitucional.API) por padrão;
        // handlers produtivos vivem em OrganizacaoInstitucional.Application —
        // incluir explicitamente para que os command/query handlers sejam descobertos.
        opts.Discovery.IncludeAssembly(typeof(OrganizacaoInstitucionalApplicationServiceRegistration).Assembly);
    });
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
