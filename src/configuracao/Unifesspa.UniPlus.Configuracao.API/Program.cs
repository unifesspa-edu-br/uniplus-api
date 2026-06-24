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
using Unifesspa.UniPlus.Configuracao.API.Errors;
using Unifesspa.UniPlus.Configuracao.API.Hateoas;
using Unifesspa.UniPlus.Configuracao.Application;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
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
// OpenAPI 3.1 (ADR-0030) — spec em /openapi/configuracao.json.
builder.Services.AddUniPlusOpenApi("configuracao", builder.Configuration);

builder.Services.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>();
builder.Services.AddDomainErrorMapper();

// HATEOAS Level 1 (ADR-0029) — builders de _links dos cadastros (UNI-REQ #587).
builder.Services.AddSingleton<IResourceLinksBuilder<CampusDto>, CampusLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<LocalOfertaDto>, LocalOfertaLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<ReferenciaReservaDemograficaDto>, ReferenciaReservaDemograficaLinksBuilder>();
builder.Services.AddSingleton<IResourceLinksBuilder<PesoAreaEnemDto>, PesoAreaEnemLinksBuilder>();

// Idempotency-Key (ADR-0027) + criptografia para entries at-rest. Filter global
// se ativa apenas em endpoints com [RequiresIdempotencyKey] — os controllers
// admin de Campus/LocalOferta o exigem.
builder.Services.AddUniPlusEncryption(builder.Configuration);
// Cursor pagination (ADR-0026) — CursorEncoder, PageRequestModelBinder e o hook
// que traduz falhas do binder para 400/410/422; usado pelos GET de listagem.
builder.Services.AddCursorPagination(builder.Configuration);
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
