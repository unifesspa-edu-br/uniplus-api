using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Selecao.API.Middleware;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure;

using Wolverine.Kafka;
using Wolverine.Postgresql;

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

string connectionString = builder.Configuration.GetConnectionString("SelecaoDb")
    ?? throw new InvalidOperationException("Connection string 'SelecaoDb' não configurada.");

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);
builder.Services.AddSelecaoApplication();
// AddSelecaoInfrastructure é configurado com a connection string lida
// eagerly do builder.Configuration. Em testes integrados, o
// CascadingApiFactory remove e re-registra o DbContext apontando para o
// Postgres efêmero — esta leitura eager fica restrita ao registro do
// DbContext do módulo, não atinge o backbone Wolverine (que lê via
// SelecaoOutboxExtension no startup, com IConfiguration final).
builder.Services.AddSelecaoInfrastructure(connectionString);

// Wolverine como backbone CQRS/messaging com outbox transacional —
// ver ADR-022, ADR-025 e ADR-026.
//
// Configuração (ADR-025 + ADR-026):
//   - PersistMessagesWithPostgresql: outbox durável no schema "wolverine"
//     do mesmo banco do módulo (SelecaoDb).
//   - UseEntityFrameworkCoreTransactions + AutoApplyTransactions:
//     atomicidade write+evento — envelope persistido na MESMA transação
//     do SaveChanges via IEnvelopeTransaction.
//   - UseDurableOutboxOnAllSendingEndpoints: rota durável é invariante.
//   - PublishDomainEventsFromEntityFrameworkCore NÃO é chamado (ADR-026):
//     drenagem de EntityBase.DomainEvents via cascading messages no
//     retorno do handler (IEnumerable<object>), não pelo scraper EF.
//   - Routing de EditalPublicadoEvent: PG queue "domain-events" +
//     tópico Kafka "edital_events" quando bootstrap configurado.
//
// A leitura de connection string e Kafka bootstrap acontece dentro do
// callback de UseWolverine no startup do host. Em testes integrados,
// os overrides chegam via env vars (ConnectionStrings__SelecaoDb,
// Kafka__BootstrapServers) — overrides via ConfigureAppConfiguration
// não propagam para WebApplicationBuilder.Configuration em minimal API.
builder.Host.UseWolverineOutboxCascading(
    builder.Configuration,
    connectionStringName: "SelecaoDb",
    configureRouting: opts =>
    {
        opts.PublishMessage<EditalPublicadoEvent>().ToPostgresqlQueue("domain-events");
        opts.ListenToPostgresqlQueue("domain-events");

        if (!string.IsNullOrWhiteSpace(builder.Configuration["Kafka:BootstrapServers"]))
        {
            opts.PublishMessage<EditalPublicadoEvent>().ToKafkaTopic("edital_events");
        }
    });
builder.Services.AddWolverineMessaging();

builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);

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
app.MapHealthChecks("/health");

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests; CA1515
// suppression lives in GlobalSuppressions.cs, not inline.
public partial class Program;
