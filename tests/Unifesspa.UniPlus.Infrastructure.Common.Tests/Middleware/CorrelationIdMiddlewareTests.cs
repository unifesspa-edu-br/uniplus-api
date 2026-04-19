namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SemHeaderNoRequest_DeveGerarUuidValido()
    {
        DefaultHttpContext context = new();
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
        accessor.Received(1).SetCorrelationId(headerResposta!);
    }

    [Fact]
    public async Task InvokeAsync_ComHeaderExistenteNoRequest_DeveReutilizarValor()
    {
        const string idExistente = "request-12345";
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idExistente;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(idExistente);
        accessor.Received(1).SetCorrelationId(idExistente);
    }

    [Fact]
    public async Task InvokeAsync_ComHeaderEmBrancoNoRequest_DeveGerarNovoUuid()
    {
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "   ";
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBeNullOrWhiteSpace();
        headerResposta.Should().NotBe("   ");
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DurantePipeline_AccessorDeveRetornarMesmoValor()
    {
        const string idExistente = "scoped-flow";
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idExistente;
        CorrelationIdAccessor accessor = new();
        string? idObservadoDentroDoPipeline = null;

        CorrelationIdMiddleware middleware = new(_ =>
        {
            idObservadoDentroDoPipeline = accessor.CorrelationId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        idObservadoDentroDoPipeline.Should().Be(idExistente);
    }

    [Fact]
    public async Task InvokeAsync_DeveChamarProximoMiddleware()
    {
        DefaultHttpContext context = new();
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        bool proximoFoiChamado = false;

        CorrelationIdMiddleware middleware = new(_ =>
        {
            proximoFoiChamado = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);

        proximoFoiChamado.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ComHeaderAcimaDoTamanhoMaximo_DeveGerarNovoUuid()
    {
        string idAbusivo = new('a', CorrelationIdMiddleware.MaxCorrelationIdLength + 1);
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idAbusivo;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBe(idAbusivo);
        headerResposta!.Length.Should().BeLessThanOrEqualTo(CorrelationIdMiddleware.MaxCorrelationIdLength);
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc\r\n\r\n[admin] linha forjada")]
    [InlineData("valor\rcom\rcarriage-return")]
    [InlineData("valor\ncom\nnewline")]
    [InlineData("valor\tcom\ttab")]
    [InlineData("valor\0com\0null")]
    public async Task InvokeAsync_ComHeaderContendoCaracteresDeControle_DeveGerarNovoUuid(string idMalicioso)
    {
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idMalicioso;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBe(idMalicioso);
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("válido-acentuado")]
    [InlineData("测试中文")]
    [InlineData("emoji-😀-teste")]
    [InlineData("espaco interno")]
    public async Task InvokeAsync_ComHeaderContendoCaracteresInvalidos_DeveGerarNovoUuid(string idInvalido)
    {
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idInvalido;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBe(idInvalido);
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DeveEnriquecerLogContextComCorrelationId()
    {
        const string id = "log-ctx-test";
        List<LogEvent> capturados = new();
        CapturingSink sink = new(capturados);

        Logger loggerDeTeste = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        ILogger loggerOriginal = Log.Logger;
        Log.Logger = loggerDeTeste;

        try
        {
            DefaultHttpContext context = new();
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = id;

            CorrelationIdMiddleware middleware = new(_ =>
            {
                Log.Information("log dentro do pipeline");
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, new CorrelationIdAccessor());
        }
        finally
        {
            Log.Logger = loggerOriginal;
            await loggerDeTeste.DisposeAsync();
        }

        capturados.Should().ContainSingle();
        capturados[0].Properties.Should().ContainKey(CorrelationIdMiddleware.LogContextProperty);
        capturados[0].Properties[CorrelationIdMiddleware.LogContextProperty]
            .ToString().Trim('"').Should().Be(id);
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
