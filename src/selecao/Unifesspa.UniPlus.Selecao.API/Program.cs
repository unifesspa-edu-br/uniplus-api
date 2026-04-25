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

using Wolverine;
using Wolverine.EntityFrameworkCore;

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
builder.Services.AddSelecaoInfrastructure(connectionString);

// Wolverine como backbone CQRS/messaging — ver ADR-022.
//
// Esta configuração entrega APENAS o backbone do bus (ICommandBus → handler).
// As policies abaixo preparam o pipeline transacional do Wolverine, mas NÃO
// implementam outbox transacional de domain events. Em particular:
//   - PersistMessagesWith* NÃO está configurado: domain events são entregues
//     in-memory pelo bus, sem persistência durável de envelopes.
//   - PublishDomainEventsFromEntityFrameworkCore NÃO está configurado:
//     EntityBase.DomainEvents NÃO é drenado automaticamente em SaveChanges.
//   - Atomicidade write+evento NÃO é garantida nesta fase.
// A adoção de outbox transacional foi reprovada no spike de #135 (ver branch
// spike/135-outbox-validation e a issue dedicada de outbox para os achados).
//
// UseEntityFrameworkCoreTransactions e AutoApplyTransactions são no-ops
// efetivos nesta fase (sem outbox, envolvem apenas o SaveChanges em uma
// transação Wolverine que não coordena nada extra). Mantidos intencionalmente
// para reduzir o delta de configuração quando a Story #158 entregar o outbox.
builder.Host.UseWolverine(opts =>
{
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();
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
