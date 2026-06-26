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
using Unifesspa.UniPlus.Configuracao.API;
using Unifesspa.UniPlus.Configuracao.Application;

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
        // Enums em wire format como nome camelCase (ex.: "poloEad", não 4) — mesmo
        // contrato do módulo Seleção. Sem isso, payload admin com enum como string
        // vira 400 e o TipoLocalOferta sairia como inteiro mágico.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// Minimal API endpoints use a separate JSON options pipeline; manter em sincronia.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services.AddEndpointsApiExplorer();
// AddDomainErrorMapper é compartilhado (consome IEnumerable<IDomainErrorRegistration>);
// o registro de erros do módulo entra via AddConfiguracaoModule.
builder.Services.AddDomainErrorMapper();

// Criptografia (entries de idempotência at-rest) e cursor pagination (ADR-0026)
// são transversais ao host.
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
builder.Services.AddConfiguracaoModule(builder.Configuration);

// Wolverine como backbone CQRS/messaging (ADR-0003/0004/0005). Os command/query
// handlers de Campus/LocalOferta vivem em Configuracao.Application — incluir o
// assembly explicitamente para que o Wolverine os descubra (o scan default é só
// o entry assembly da API). Sem roteamento Kafka em V1.
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "ConfiguracaoDb",
    configureRouting: opts =>
        opts.Discovery.IncludeAssembly(typeof(ConfiguracaoApplicationServiceRegistration).Assembly));
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
