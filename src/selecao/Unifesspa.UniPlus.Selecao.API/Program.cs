using Serilog;

using Unifesspa.UniPlus.Selecao.API.Middleware;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();

string connectionString = builder.Configuration.GetConnectionString("SelecaoDb")
    ?? throw new InvalidOperationException("Connection string 'SelecaoDb' não configurada.");

builder.Services.AddSelecaoApplication();
builder.Services.AddSelecaoInfrastructure(connectionString);

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
