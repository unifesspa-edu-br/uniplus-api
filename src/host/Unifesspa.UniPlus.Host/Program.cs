using Serilog;

using Unifesspa.UniPlus.Configuracao.API;
using Unifesspa.UniPlus.Configuracao.Application;
using Unifesspa.UniPlus.Ingresso.API;
using Unifesspa.UniPlus.Publicacoes.API;
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
using Unifesspa.UniPlus.Selecao.API;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Infrastructure.Messaging;

// Composition root do monólito modular. Compõe os 5 módulos internos
// (Selecao, Ingresso, Configuracao, OrganizacaoInstitucional) num processo único,
// apontando todos para o banco `uniplus` (schema-por-módulo). Geo e Portal seguem
// deploys separados. Este assembly é o único que depende de múltiplos módulos
// (composition root) — isento do fitness R8.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

const string nomeServico = "uniplus-monolito";

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration, nomeServico));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services.AddEndpointsApiExplorer();

// Isola os documentos OpenAPI por módulo no processo único: atribui GroupName
// por namespace de controller para que cada /openapi/{modulo}.json contenha
// apenas os endpoints do seu módulo (+ compartilhados). Só faz sentido no host
// (co-hosting); os módulos standalone não recebem e mantêm seus baselines.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(
    options => options.Conventions.Add(new Unifesspa.UniPlus.Host.ModuleApiGroupingConvention()));

// --- Cross-cutting compartilhado (registrado uma vez no host) ---
builder.Services.AddDomainErrorMapper();
builder.Services.AddUniPlusEncryption(builder.Configuration);
builder.Services.AddCursorPagination(builder.Configuration);
builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);
builder.Services.AdicionarObservabilidade(nomeServico, builder.Configuration, builder.Environment);
builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusStorage(builder.Configuration, builder.Environment);
builder.Services.AddUniPlusCache(builder.Configuration, builder.Environment);

// --- Módulos do monólito (self-describing; cada um traz seu OpenAPI doc, erros,
// HATEOAS, idempotência, Application+Infrastructure e migrations on startup) ---
builder.Services.AddConfiguracaoModule(builder.Configuration);
builder.Services.AddOrganizacaoInstitucionalModule(builder.Configuration);
builder.Services.AddSelecaoModule(builder.Configuration);
builder.Services.AddIngressoModule(builder.Configuration);
builder.Services.AddPublicacoesModule(builder.Configuration);

// Health checks: os 5 módulos compartilham o banco `uniplus`, então um
// único check de banco + infra (Redis/MinIO/Kafka/OIDC) cobre o processo.
builder.Services.AddUniPlusHealthChecks(builder.Configuration, connectionStringName: "UniPlusDb");

// --- Wolverine consolidado: UMA instância, outbox no banco `uniplus`
// (schema `wolverine`), compondo o discovery de handlers dos módulos. As
// migrations on startup (registradas acima nos Add*Module) precedem o runtime
// Wolverine (invariante #419). ---

// Mensageria do Selecao (Kafka/Schema Registry — ADR-0051 — + routing): o módulo
// é dono do seu wiring (SelecaoMessagingRegistration); o host registra o Schema
// Registry e compõe o configurador de routing na instância única de Wolverine.
Action<Wolverine.WolverineOptions> configurarSelecaoRouting =
    builder.Services.AddSelecaoMessaging(builder.Configuration, builder.Environment);

builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "UniPlusDb",
    configureRouting: opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(ConfiguracaoApplicationServiceRegistration).Assembly);
        opts.Discovery.IncludeAssembly(typeof(OrganizacaoInstitucionalApplicationServiceRegistration).Assembly);
        opts.Discovery.IncludeAssembly(typeof(CriarProcessoSeletivoCommand).Assembly);
        opts.Discovery.IncludeAssembly(typeof(ProcessoPublicadoToKafkaCascadeHandler).Assembly);

        // Opt-ins de codegen (ADR-0098): sob ServiceLocationPolicy.NotAllowed, cada
        // módulo declara as UoW que usam service location intencionalmente (forwarding
        // para a MESMA instância de DbContext — ADR-0004). Cada módulo é dono do seu
        // opt-in (OCP/SRP); o host apenas compõe — WolverineOutboxConfiguration
        // permanece agnóstico dos tipos de módulo (Clean Arch).
        SelecaoCodegenRegistration.ConfigurarCodegenWolverine(opts);
        ConfiguracaoCodegenRegistration.ConfigurarCodegenWolverine(opts);
        OrganizacaoInstitucionalCodegenRegistration.ConfigurarCodegenWolverine(opts);

        // Routing do Selecao (PG queue domain-events + Kafka processo_seletivo_events) —
        // religa a mensageria externa antes deferida no monólito.
        configurarSelecaoRouting(opts);
    });
builder.Services.AddWolverineMessaging();

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

await app.RunAsync().ConfigureAwait(false);

// Necessário para WebApplicationFactory<Program> nos testes de integração do host.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design", "CA1515:Consider making public types internal",
    Justification = "Program público exigido por WebApplicationFactory<Program>.")]
public partial class Program;
