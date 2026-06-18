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
using Unifesspa.UniPlus.Geo.API.Errors;
using Unifesspa.UniPlus.Geo.API.Hateoas;
using Unifesspa.UniPlus.Geo.Application;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Infrastructure;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// service.name canônico — single source of truth em UniPlusServiceNames evita
// drift entre logs (Serilog/Loki) e traces (Tempo). Ver Selecao.API/Program.cs.
const string nomeServico = UniPlusServiceNames.Geo;

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
// OpenAPI 3.1 (ADR-0030) — spec em /openapi/geo.json.
builder.Services.AddUniPlusOpenApi("geo", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, GeoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// Criptografia (entries de idempotency at-rest) + cursor pagination (ADR-0026/0031).
// AddCursorPagination já entra na fundação porque as Stories de API do Geo
// (listagens de localidades) terão endpoints paginados.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);
// Idempotency-Key (ADR-0027) — store EF adjacente ao GeoDbContext, filter global
// que se ativa apenas em endpoints com [RequiresIdempotencyKey].
builder.Services.AddIdempotency<GeoDbContext>(builder.Configuration);

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);

// Observabilidade (ADR-0018) — tracing + metrics via OpenTelemetry SDK.
builder.Services.AdicionarObservabilidade(nomeServico, builder.Configuration, builder.Environment);

builder.Services.AddGeoApplication();
builder.Services.AddGeoInfrastructure();

// HATEOAS Level 1 (ADR-0029) do registro de execução do ETL (#674).
builder.Services.AddSingleton<IResourceLinksBuilder<ImportacaoGeoDto>, ImportacaoGeoLinksBuilder>();

// HATEOAS Level 1 (ADR-0029) das leituras de reference data (#675): Estado e Cidade.
builder.Services.AddSingleton<IResourceLinksBuilder<EstadoDto>, EstadoLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<CidadeResumoDto>, CidadeResumoLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<CidadeDetalheDto>, CidadeDetalheLinksBuilder>();

// Migrations EF Core aplicadas no host StartAsync via IHostedService.
// Registrado ANTES de UseWolverineOutboxCascading + AddWolverineMessaging
// (invariante #419) — fitness MigrationBeforeWolverineRuntimeOrderTests cobre
// os entry points com a regra.
builder.Services.AddDbContextMigrationsOnStartup<GeoDbContext>();

// Gatilhos hospedados do ETL periódico (#674): worker que executa as cargas
// enfileiradas + seed de dev. Registrado APÓS as migrations de propósito — seed e
// reconciliação do worker tocam o banco, que precisa já ter a tabela
// geo_importacao_execucao (ServicesStartConcurrently=false ⇒ ordem de start = ordem
// de registro). Mantém-se antes do Wolverine, coerente com o invariante de ordem.
builder.Services.AddGeoEtlGatilhos(builder.Configuration, builder.Environment);

// Wolverine como backbone CQRS/messaging (ADR-0003/0004/0005). Sem roteamento
// específico em V1 — handlers de localidade entram nas Stories de API; o
// Discovery.IncludeAssembly do Geo.Application já deixa o scanner preparado.
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "GeoDb",
    configureRouting: opts =>
        opts.Discovery.IncludeAssembly(typeof(GeoApplicationAssemblyMarker).Assembly));
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// Health checks: Postgres + Redis + MinIO + Kafka + OIDC.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "GeoDb");

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
