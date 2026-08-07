namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using System.Diagnostics;

using AwesomeAssertions;

using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Wolverine;

/// <summary>
/// Cobertura unitária do <see cref="CorrelationIdEnvelopeMiddleware"/> — terceiro
/// componente da ADR-0052. Cenários cobrem: header válido upstream (preservado),
/// header inválido (regenerado), header ausente (fallback para
/// <see cref="ICorrelationIdAccessor"/> ambient ou GUID novo), reescrita para
/// propagação outgoing e Activity tag.
/// </summary>
public sealed class CorrelationIdEnvelopeMiddlewareTests : IDisposable
{
    // Logger LOCAL com sink capturador — não mutamos Log.Logger global porque
    // xUnit roda classes de teste em paralelo por default (class-level parallelism)
    // e a mutação process-wide expõe o sink a outras suites, gerando flakes em
    // assertions de cardinalidade (ContainSingle/HaveCount). Como Serilog.Context.LogContext
    // armazena valores em AsyncLocal<T>, qualquer logger com Enrich.FromLogContext()
    // consegue ler o valor empurrado dentro do escopo — não precisamos do logger global.
    private readonly CapturingSink _sink = new();
    private readonly Logger _logger;
    private readonly StubAccessor _accessorVazio = new(corrente: null);

    public CorrelationIdEnvelopeMiddlewareTests()
    {
        _logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(_sink)
            .CreateLogger();
    }

    public void Dispose() => _logger.Dispose();

    // ─── CA-04: header upstream válido é preservado ─────────────────────────

    [Fact]
    public void Before_DadoHeaderValido_DevePreservarValorOriginal()
    {
        const string correlationIdUpstream = "abc-123_DEF.456";
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = correlationIdUpstream;

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]
                .Should().Be(correlationIdUpstream);
        }
    }

    [Fact]
    public void Before_DadoHeaderValido_DevePropagarParaSerilogLogContext()
    {
        const string correlationIdUpstream = "fluxo-edital-publicado-001";
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = correlationIdUpstream;

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            _logger.Information("trabalhando no handler");
        }

        _sink.Eventos.Should().ContainSingle();
        ((ScalarValue)_sink.Eventos[0].Properties[CorrelationIdEnvelopeMiddleware.LogContextProperty]).Value
            .Should().Be(correlationIdUpstream);
    }

    // ─── P1 fix: prioridade entre header explícito × ambient HTTP ───────────

    [Fact]
    public void Before_ComHeaderEAmbient_DevePreferirHeaderExplicito()
    {
        // Cross-host (Kafka): header explícito carrega o id canônico negociado pelo
        // produtor. Ambient deve ser ignorado para evitar substituição acidental do
        // id que viajou no envelope (ex.: handler que processa Kafka dentro de
        // outro request HTTP onde o accessor já está populado).
        const string headerExplicito = "vindo-do-kafka";
        StubAccessor accessor = new(corrente: "valor-ambient-residual");
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = headerExplicito;

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, accessor))
        {
            envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]
                .Should().Be(headerExplicito);
            _logger.Information("trabalhando no handler");
        }

        ((ScalarValue)_sink.Eventos[0].Properties[CorrelationIdEnvelopeMiddleware.LogContextProperty]).Value
            .Should().Be(headerExplicito);
    }

    [Fact]
    public void Before_SemHeaderMasComAmbient_DeveReusarAmbientDoHttp()
    {
        // Fluxo in-process: controller HTTP chama ICommandBus.Send → Wolverine cria
        // envelope local sem o header. Sem este fallback, geraríamos GUID novo e
        // sobrescreveríamos o CorrelationId já empurrado pelo CorrelationIdMiddleware
        // HTTP no AsyncLocal do CorrelationIdAccessor — quebraria o drill-down
        // end-to-end (logs HTTP em X, logs do handler em Y).
        const string ambientDoHttp = "req-abc-123";
        StubAccessor accessor = new(corrente: ambientDoHttp);
        Envelope envelope = new();

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, accessor))
        {
            envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]
                .Should().Be(ambientDoHttp);
            _logger.Information("trabalhando no handler");
        }

        ((ScalarValue)_sink.Eventos[0].Properties[CorrelationIdEnvelopeMiddleware.LogContextProperty]).Value
            .Should().Be(ambientDoHttp);
    }

    [Fact]
    public void Before_ComAmbientInvalido_DeveIgnorarAmbientEGerarNovoGuid()
    {
        // Ambient pode ter sido populado por código fora do CorrelationIdMiddleware
        // (caller exótico ou regressão). Aplica a mesma validação de regex do header
        // para não vazar input malformado para os dashboards.
        StubAccessor accessor = new(corrente: "ambient com espaço");
        Envelope envelope = new();

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, accessor))
        {
            string idAposMiddleware = envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]!;
            idAposMiddleware.Should().NotBe("ambient com espaço");
            Guid.TryParse(idAposMiddleware, out _).Should().BeTrue();
        }
    }

    // ─── CA-03: header ausente E sem ambient — gera novo GUID ──────────────

    [Fact]
    public void Before_SemHeaderESemAmbient_DeveGerarGuidELogar()
    {
        Envelope envelope = new();

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            string idGerado = envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]!;
            idGerado.Should().NotBeNullOrWhiteSpace();
            idGerado.Should().MatchRegex("^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$");
            Guid.TryParse(idGerado, out _).Should().BeTrue();
        }
    }

    [Fact]
    public void Before_SemHeaderESemAmbient_DevePropagarGuidParaLogContext()
    {
        Envelope envelope = new();

        string idGerado;
        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            idGerado = envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]!;
            _logger.Information("trabalhando no handler");
        }

        _sink.Eventos.Should().ContainSingle();
        ((ScalarValue)_sink.Eventos[0].Properties[CorrelationIdEnvelopeMiddleware.LogContextProperty]).Value
            .Should().Be(idGerado);
    }

    // ─── CA-03: header inválido — rejeita e regenera ────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("contém espaço inválido")]
    [InlineData("char-com-acentuação")]
    [InlineData("control\r\nchar")]
    // NUL byte literal embed em InlineData faria git renderizar o arquivo como binário
    // e analisadores text-based quebrariam. O caso de NUL byte
    // específico é coberto pela classe de equivalência 'control char' via
    // [InlineData("control\\r\\nchar")] acima — re-introduzir NUL literal aqui só
    // recriaria esse problema de tooling.
    [InlineData("muito-longo-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Before_DadoHeaderInvalido_DeveRejeitarEGerarNovoGuid(string valorInvalido)
    {
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = valorInvalido;

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            string idAposMiddleware = envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName]!;
            idAposMiddleware.Should().NotBe(valorInvalido);
            Guid.TryParse(idAposMiddleware, out _).Should().BeTrue(
                "input inválido deve ser substituído por GUID — previne log injection e poluição de dashboards");
        }
    }

    // ─── CA-05/CA-06: reescrita para propagação outgoing ───────────────────

    [Fact]
    public void Before_DeveReescreverHeaderNoEnvelope_GarantindoPropagacaoOutgoing()
    {
        // Garantia explícita: o middleware reescreve sempre — assim
        // PropagateIncomingHeaderToOutgoing repassa o id canônico para qualquer
        // mensagem publicada no escopo do handler (cascading, IMessageBus.PublishAsync).
        Envelope envelope = new();

        using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
        {
            envelope.Headers.Should().ContainKey(CorrelationIdEnvelopeMiddleware.HeaderName);
        }
    }

    // ─── Activity tag (drill-down Loki ↔ Tempo no Grafana) ─────────────────

    [Fact]
    public void Before_DadoActivityAtiva_DevePropagarCorrelationIdComoTag()
    {
        ActivitySource source = new(nameof(CorrelationIdEnvelopeMiddlewareTests));
        ActivityListener listener = new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        try
        {
            using Activity activity = source.StartActivity("teste")!;
            const string idCanonico = "fluxo-001";
            Envelope envelope = new();
            envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = idCanonico;

            using (CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio))
            {
                activity.GetTagItem(CorrelationIdEnvelopeMiddleware.ActivityTagName)
                    .Should().Be(idCanonico);
            }
        }
        finally
        {
            listener.Dispose();
            source.Dispose();
        }
    }

    [Fact]
    public void Before_SemActivityAtiva_NaoDeveLancarExcecao()
    {
        // Suites HTTP-only com Observability:Enabled=false têm Activity.Current null
        // (nenhum listener OTel wired). O middleware deve ser tolerante a esse caso.
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = "fluxo-002";

        Action acao = () =>
        {
            using IDisposable scope = CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio);
        };

        acao.Should().NotThrow();
    }

    // ─── Disposal contract (ciclo escopo LogContext) ───────────────────────

    [Fact]
    public void Finally_DadoScopeRetornadoPorBefore_DeveDisporELiberarLogContextProperty()
    {
        // Garante que após o Finally, a propriedade CorrelationId não vaza para
        // execuções subsequentes no mesmo thread — evita confusão entre handlers
        // executados sequencialmente pelo mesmo runtime.
        Envelope envelope = new();
        envelope.Headers[CorrelationIdEnvelopeMiddleware.HeaderName] = "escopo-handler-A";

        IDisposable scope = CorrelationIdEnvelopeMiddleware.Before(envelope, _accessorVazio);
        _logger.Information("dentro do escopo");
        CorrelationIdEnvelopeMiddleware.Finally(scope);
        _logger.Information("fora do escopo");

        _sink.Eventos.Should().HaveCount(2);
        ((ScalarValue)_sink.Eventos[0].Properties[CorrelationIdEnvelopeMiddleware.LogContextProperty]).Value
            .Should().Be("escopo-handler-A");
        _sink.Eventos[1].Properties.Should().NotContainKey(CorrelationIdEnvelopeMiddleware.LogContextProperty,
            "Finally deve dispor o escopo, liberando a propriedade do LogContext do thread");
    }

    [Fact]
    public void Finally_DadoScopeNulo_DeveLancarArgumentNullException()
    {
        Action acao = () => CorrelationIdEnvelopeMiddleware.Finally(null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Before_DadoEnvelopeNulo_DeveLancarArgumentNullException()
    {
        Action acao = () => CorrelationIdEnvelopeMiddleware.Before(null!, _accessorVazio);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Before_DadoAccessorNulo_DeveLancarArgumentNullException()
    {
        Envelope envelope = new();

        Action acao = () => CorrelationIdEnvelopeMiddleware.Before(envelope, null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    // ─── Invariante regex espelha CorrelationIdMiddleware HTTP ─────────────

    [Fact]
    public void HeaderName_DeveSerUniplusCorrelationIdParaWireFormatCanonico()
    {
        CorrelationIdEnvelopeMiddleware.HeaderName.Should().Be("uniplus.correlation-id");
    }

    [Fact]
    public void LogContextProperty_DeveSerCompartilhadaComCorrelationIdMiddleware()
    {
        // Invariante crítica: queries LogQL {CorrelationId="..."} reconstroem fluxos
        // HTTP-only e fluxos cross-Kafka sem ramificar — ambas as paredes (HTTP e Kafka)
        // devem empurrar a MESMA propriedade Serilog.
        CorrelationIdEnvelopeMiddleware.LogContextProperty
            .Should().Be(CorrelationIdMiddleware.LogContextProperty);
    }

    [Fact]
    public void ActivityTagName_DeveSerCompartilhadaComCorrelationIdMiddleware()
    {
        CorrelationIdEnvelopeMiddleware.ActivityTagName
            .Should().Be(CorrelationIdMiddleware.ActivityTagName);
    }

    [Fact]
    public void MaxCorrelationIdLength_DeveSerCompartilhadaComCorrelationIdMiddleware()
    {
        CorrelationIdEnvelopeMiddleware.MaxCorrelationIdLength
            .Should().Be(CorrelationIdMiddleware.MaxCorrelationIdLength);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            Eventos.Add(logEvent);
        }
    }

    /// <summary>Stub de <see cref="ICorrelationIdAccessor"/> sem dependência de DI/HttpContext.</summary>
    private sealed class StubAccessor(string? corrente) : ICorrelationIdAccessor
    {
        public string? CorrelationId { get; } = corrente;
    }
}
