using Serilog;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Profile;
using Unifesspa.UniPlus.Selecao.API.Middleware;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Infrastructure;

using JasperFx.Resources;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

using Wolverine;
using Wolverine.EntityFrameworkCore;
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

builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCorrelationIdAccessor();
builder.Services.AddRequestLogging(builder.Configuration);
builder.Services.AddSelecaoApplication();
builder.Services.AddSelecaoInfrastructure();

// Wolverine como backbone CQRS/messaging — ver ADR-022.
// Outbox transacional (issue #135): PersistMessagesWithPostgresql armazena
// envelopes em wolverine.wolverine_outgoing_envelopes /
// wolverine.wolverine_incoming_envelopes. AddResourceSetupOnStartup
// (JasperFx.Resources) provisiona o schema em runtime — sem migration EF Core
// (ver issue #155 para a estratégia formal de migrations das entidades de domínio).
//
// A connection string é lida dentro do callback (não capturada em closure) para
// que WebApplicationFactory.ConfigureAppConfiguration consiga sobrescrever via
// Testcontainers nos testes de integração — o callback executa durante o Build,
// depois das fontes de configuração serem mescladas.
builder.Host.UseWolverine(opts =>
{
    string connectionString = builder.Configuration.GetConnectionString("SelecaoDb")
        ?? throw new InvalidOperationException("Connection string 'SelecaoDb' não configurada.");
    opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
    opts.UseEntityFrameworkCoreTransactions();
    opts.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(e => e.DomainEvents);
    opts.Policies.AutoApplyTransactions();
    // SPIKE V3: rota e durabilidade vêm via IWolverineExtension registrada no test factory.
});
builder.Services.AddResourceSetupOnStartup();
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
