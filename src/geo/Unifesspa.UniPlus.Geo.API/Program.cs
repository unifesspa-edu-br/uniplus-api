using Microsoft.AspNetCore.OpenApi;

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
using Unifesspa.UniPlus.Geo.API;
using Unifesspa.UniPlus.Geo.API.Errors;
using Unifesspa.UniPlus.Geo.API.Formatting;
using Unifesspa.UniPlus.Geo.API.Hateoas;
using Unifesspa.UniPlus.Geo.API.OpenApi;
using Unifesspa.UniPlus.Geo.Application;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Infrastructure;
using Unifesspa.UniPlus.Geo.Infrastructure.Cep;
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
// Marca lat/long/raioKm da proximidade (#678) como required no contrato — são
// validados em runtime (400 se ausentes), mas como query params double? o
// ApiExplorer os descreveria como opcionais. AddOpenApi para o mesmo documento é
// aditivo: anexa o transformer ao pipeline do doc "geo".
builder.Services.AddOpenApi("geo", options =>
    options.AddOperationTransformer<ProximidadeRequiredParametrosTransformer>());

builder.Services.AddSingleton<IDomainErrorRegistration, GeoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// Criptografia (entries de idempotency at-rest) + cursor pagination (ADR-0026/0031).
// AddCursorPagination já entra na fundação porque as Stories de API do Geo
// (listagens de localidades) terão endpoints paginados.
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);
// Idempotency-Key (ADR-0027) — store EF adjacente ao GeoDbContext, filter global
// que se ativa apenas em endpoints com [RequiresIdempotencyKey].
builder.Services.AddIdempotency<GeoDbContext, GeoApiAssemblyMarker>(builder.Configuration);

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
builder.Services.AddSingleton<IResourceLinksBuilder<DistritoDto>, DistritoLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<BairroDto>, BairroLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<LogradouroResumoDto>, LogradouroResumoLinksBuilder>();

// HATEOAS Level 1 (ADR-0029) do lookup de CEP (#676): _links para cidade e estado.
builder.Services.AddSingleton<IResourceLinksBuilder<CepResolvidoDto>, CepResolvidoLinksBuilder>();

// HATEOAS Level 1 (ADR-0029) da proximidade geoespacial (#678): _links para cidade
// (e CEP no caso de logradouro). Limites (teto de raio, default/teto de top-N)
// configuráveis via seção Geo:Proximidade (GeoProximidadeOptions), com defaults.
builder.Services.AddSingleton<IResourceLinksBuilder<CidadeProximaDto>, CidadeProximaLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<LogradouroProximoDto>, LogradouroProximoLinksBuilder>();
// Validação fail-fast dos limites: config inválida (ex.: LimitMax=0) faria
// Take(limit) receber 0 e esvaziar buscas válidas — falha no boot, não em runtime.
builder.Services.AddOptions<GeoProximidadeOptions>()
    .Bind(builder.Configuration.GetSection(GeoProximidadeOptions.SectionName))
    .Validate(
        static o => o.LimitesValidos(),
        "Geo:Proximidade — RaioMaxKm > 0, LimitMax >= 1 e 1 <= LimitPadrao <= LimitMax.")
    .ValidateOnStart();

// TTL configurável do cache-aside de CEP (#676) — default 24h; também a memoização
// em processo do selo de versão (#703, SeloTtl default 15s). Ambos em GeoCepCacheOptions.
builder.Services.Configure<GeoCepCacheOptions>(
    builder.Configuration.GetSection(GeoCepCacheOptions.SectionName));
// Teto de fan-out de logradouros do reader de CEP (#705) — default 50 (GeoCepLookupOptions).
builder.Services.Configure<GeoCepLookupOptions>(
    builder.Configuration.GetSection(GeoCepLookupOptions.SectionName));

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
