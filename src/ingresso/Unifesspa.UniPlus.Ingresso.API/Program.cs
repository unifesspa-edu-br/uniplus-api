using Serilog;

using Unifesspa.UniPlus.Infrastructure.Common.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Common.Logging;
using Unifesspa.UniPlus.Infrastructure.Common.Middleware;
using Unifesspa.UniPlus.Ingresso.API.Middleware;
using Unifesspa.UniPlus.Ingresso.Application.Mappings;
using Unifesspa.UniPlus.Ingresso.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ConfigurarSerilog(context.Configuration));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();

string connectionString = builder.Configuration.GetConnectionString("IngressoDb")
    ?? throw new InvalidOperationException("Connection string 'IngressoDb' não configurada.");

builder.Services.AddCorrelationIdAccessor();
builder.Services.AddIngressoApplication();
builder.Services.AddIngressoInfrastructure(connectionString);

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
