namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
        accessor.Received(1).SetCorrelationId(headerResposta!);
    }

    [Fact]
    public async Task InvokeAsync_ComHeaderExistenteNoRequest_DeveReutilizarValor()
    {
        const string idExistente = "request-12345";
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idExistente;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(idExistente);
        accessor.Received(1).SetCorrelationId(idExistente);
    }

    [Fact]
    public async Task InvokeAsync_ComHeaderEmBrancoNoRequest_DeveGerarNovoUuid()
    {
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "   ";
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBeNullOrWhiteSpace();
        headerResposta.Should().NotBe("   ");
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DurantePipeline_AccessorDeveRetornarMesmoValor()
    {
        // Instanciação direta é segura aqui porque o AsyncLocal que guarda o
        // valor é estático: qualquer CorrelationIdAccessor enxerga o mesmo
        // fluxo lógico. Em produção a instância vem via DI como Singleton.
        const string idExistente = "scoped-flow";
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idExistente;
        CorrelationIdAccessor accessor = new();
        string? idObservadoDentroDoPipeline = null;

        CorrelationIdMiddleware middleware = new(_ =>
        {
            idObservadoDentroDoPipeline = accessor.CorrelationId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

        idObservadoDentroDoPipeline.Should().Be(idExistente);
    }

    [Fact]
    public async Task InvokeAsync_DeveChamarProximoMiddleware()
    {
        (DefaultHttpContext context, _) = CriarContexto();
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
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idAbusivo;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

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
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idMalicioso;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

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
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = idInvalido;
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);
        await dispararOnStarting();

        string? headerResposta = context.Response.Headers[CorrelationIdMiddleware.HeaderName];
        headerResposta.Should().NotBe(idInvalido);
        Guid.TryParse(headerResposta, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_HeaderEscritoSomenteNoFlushDaResposta()
    {
        // Garante que a escrita foi registrada via OnStarting e não aplicada
        // imediatamente. Protege o invariante contra alguém desfazer a
        // mudança e voltar para a escrita síncrona.
        (DefaultHttpContext context, Func<Task> dispararOnStarting) = CriarContexto();
        ICorrelationIdWriter accessor = Substitute.For<ICorrelationIdWriter>();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, accessor);

        context.Response.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName).Should().BeFalse(
            "o header só deve ser aplicado quando o flush da resposta disparar OnStarting");

        await dispararOnStarting();

        context.Response.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DeveEnriquecerLogContextComCorrelationId()
    {
        // LogContext.PushProperty (chamada pelo middleware) atua sobre um
        // AsyncLocal estático, não sobre uma instância de ILogger. Portanto
        // qualquer logger com Enrich.FromLogContext() enxerga a propriedade,
        // inclusive um logger local. Isso permite testar o enrichment sem
        // substituir o singleton Log.Logger — swap global seria frágil se o
        // xUnit passasse a paralelizar testes entre classes.
        const string id = "log-ctx-test";
        List<LogEvent> capturados = new();
        CapturingSink sink = new(capturados);

        await using Logger loggerDeTeste = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        (DefaultHttpContext context, _) = CriarContexto();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = id;

        CorrelationIdMiddleware middleware = new(_ =>
        {
            loggerDeTeste.Information("log dentro do pipeline");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new CorrelationIdAccessor());

        capturados.Should().ContainSingle();
        capturados[0].Properties.Should().ContainKey(CorrelationIdMiddleware.LogContextProperty);
        capturados[0].Properties[CorrelationIdMiddleware.LogContextProperty]
            .ToString().Trim('"').Should().Be(id);
    }

    // Cria um DefaultHttpContext com IHttpResponseFeature customizado que
    // captura os callbacks registrados em Response.OnStarting (o feature
    // default lança NotSupportedException). Retorna também um delegate que
    // dispara os callbacks na ordem LIFO, emulando o comportamento de
    // Kestrel ao iniciar o flush da resposta.
    private static (DefaultHttpContext, Func<Task>) CriarContexto()
    {
        TestHttpResponseFeature feature = new();
        DefaultHttpContext context = new();
        context.Features.Set<IHttpResponseFeature>(feature);
        return (context, feature.DispararOnStartingAsync);
    }

    private sealed class TestHttpResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = new();

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            ArgumentNullException.ThrowIfNull(state);
            _callbacks.Add((callback, state));
        }

        public async Task DispararOnStartingAsync()
        {
            // LIFO conforme Kestrel
            for (int i = _callbacks.Count - 1; i >= 0; i--)
            {
                await _callbacks[i].Callback(_callbacks[i].State);
            }
        }
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
