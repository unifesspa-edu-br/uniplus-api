namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_DeveChamarProximoMiddleware()
    {
        DefaultHttpContext context = CriarContexto("GET", "/api/recurso");
        bool proximoFoiChamado = false;

        RequestLoggingMiddleware middleware = new(
            _ =>
            {
                proximoFoiChamado = true;
                return Task.CompletedTask;
            },
            NullLogger<RequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        proximoFoiChamado.Should().BeTrue();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(399)]
    public async Task InvokeAsync_StatusAbaixo400_DeveLogarNoNivelInformation(int statusCode)
    {
        List<LogEvent> eventos = await ExecutarERetornarLogsAsync("GET", "/api/x", statusCode);

        eventos.Should().ContainSingle();
        eventos[0].Level.Should().Be(LogEventLevel.Information);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(499)]
    public async Task InvokeAsync_Status4xx_DeveLogarNoNivelWarning(int statusCode)
    {
        List<LogEvent> eventos = await ExecutarERetornarLogsAsync("POST", "/api/x", statusCode);

        eventos.Should().ContainSingle();
        eventos[0].Level.Should().Be(LogEventLevel.Warning);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task InvokeAsync_Status5xx_DeveLogarNoNivelError(int statusCode)
    {
        List<LogEvent> eventos = await ExecutarERetornarLogsAsync("GET", "/api/x", statusCode);

        eventos.Should().ContainSingle();
        eventos[0].Level.Should().Be(LogEventLevel.Error);
    }

    [Fact]
    public async Task InvokeAsync_DeveIncluirPropriedadesMethodPathStatusEDuracao()
    {
        List<LogEvent> eventos = await ExecutarERetornarLogsAsync("POST", "/api/editais", 201);

        LogEvent log = eventos.Should().ContainSingle().Subject;
        log.Properties["Method"].ToString().Trim('"').Should().Be("POST");
        log.Properties["Path"].ToString().Trim('"').Should().Be("/api/editais");
        log.Properties["StatusCode"].ToString().Should().Be("201");
        long elapsed = long.Parse(log.Properties["ElapsedMs"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        elapsed.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeAsync_ComQueryStringSensivel_DeveRegistrarValorMascarado()
    {
        List<LogEvent> eventos = await ExecutarERetornarLogsAsync(
            "GET",
            "/api/candidatos",
            200,
            query: "?cpf=12345678900&page=1");

        LogEvent log = eventos.Should().ContainSingle().Subject;
        string queryValue = log.Properties["Query"].ToString().Trim('"');
        queryValue.Should().Be("?cpf=***&page=1");
        queryValue.Should().NotContain("12345678900");
    }

    [Fact]
    public async Task InvokeAsync_QuandoProximoLancaExcecao_DeveRegistrarLogEPropagar()
    {
        DefaultHttpContext context = CriarContexto("GET", "/api/x");
        (Logger logger, List<LogEvent> eventos) = CriarLoggerSerilog();

        try
        {
            using SerilogLoggerFactory factory = new(logger);
            RequestLoggingMiddleware middleware = new(
                _ => throw new InvalidOperationException("falha simulada"),
                factory.CreateLogger<RequestLoggingMiddleware>());

            Func<Task> acao = () => middleware.InvokeAsync(context);

            await acao.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            await logger.DisposeAsync();
        }

        eventos.Should().ContainSingle();
    }

    [Fact]
    public async Task InvokeAsync_DentroDoEscopoDeLogContext_DeveEnriquecerComCorrelationId()
    {
        // Middleware vive dentro do escopo criado por CorrelationIdMiddleware,
        // que faz LogContext.PushProperty("CorrelationId", ...). O enrichment
        // aplica-se automaticamente a qualquer log emitido dentro desse escopo —
        // teste confirma que o log do request carrega o correlation id.
        const string correlationId = "req-logging-test-id";
        DefaultHttpContext context = CriarContexto("GET", "/api/x");
        (Logger logger, List<LogEvent> eventos) = CriarLoggerSerilog();

        try
        {
            using SerilogLoggerFactory factory = new(logger);
            RequestLoggingMiddleware middleware = new(
                _ =>
                {
                    context.Response.StatusCode = 200;
                    return Task.CompletedTask;
                },
                factory.CreateLogger<RequestLoggingMiddleware>());

            using (LogContext.PushProperty(CorrelationIdMiddleware.LogContextProperty, correlationId))
            {
                await middleware.InvokeAsync(context);
            }
        }
        finally
        {
            await logger.DisposeAsync();
        }

        LogEvent log = eventos.Should().ContainSingle().Subject;
        log.Properties.Should().ContainKey(CorrelationIdMiddleware.LogContextProperty);
        log.Properties[CorrelationIdMiddleware.LogContextProperty]
            .ToString().Trim('"').Should().Be(correlationId);
    }

    [Fact]
    public async Task Construtor_ComNext_OuLogger_Nulo_DeveLancar()
    {
        Func<RequestLoggingMiddleware> criarSemNext = () => new RequestLoggingMiddleware(null!, NullLogger<RequestLoggingMiddleware>.Instance);
        Func<RequestLoggingMiddleware> criarSemLogger = () => new RequestLoggingMiddleware(_ => Task.CompletedTask, null!);

        criarSemNext.Should().Throw<ArgumentNullException>();
        criarSemLogger.Should().Throw<ArgumentNullException>();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task InvokeAsync_ComContextoNulo_DeveLancarArgumentNullException()
    {
        RequestLoggingMiddleware middleware = new(
            _ => Task.CompletedTask,
            NullLogger<RequestLoggingMiddleware>.Instance);

        Func<Task> acao = () => middleware.InvokeAsync(null!);

        await acao.Should().ThrowAsync<ArgumentNullException>();
    }

    private static async Task<List<LogEvent>> ExecutarERetornarLogsAsync(
        string method,
        string path,
        int statusCode,
        string? query = null)
    {
        DefaultHttpContext context = CriarContexto(method, path, query);
        (Logger logger, List<LogEvent> eventos) = CriarLoggerSerilog();

        try
        {
            using SerilogLoggerFactory factory = new(logger);
            RequestLoggingMiddleware middleware = new(
                _ =>
                {
                    context.Response.StatusCode = statusCode;
                    return Task.CompletedTask;
                },
                factory.CreateLogger<RequestLoggingMiddleware>());

            await middleware.InvokeAsync(context);
        }
        finally
        {
            await logger.DisposeAsync();
        }

        return eventos;
    }

    private static DefaultHttpContext CriarContexto(string method, string path, string? query = null)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        if (!string.IsNullOrEmpty(query))
        {
            context.Request.QueryString = new QueryString(query);
        }

        return context;
    }

    private static (Logger Logger, List<LogEvent> Eventos) CriarLoggerSerilog()
    {
        List<LogEvent> eventos = new();
        CapturingSink sink = new(eventos);
        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, eventos);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly List<LogEvent> _eventos;

        public CapturingSink(List<LogEvent> eventos)
        {
            _eventos = eventos;
        }

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            _eventos.Add(logEvent);
        }
    }
}
