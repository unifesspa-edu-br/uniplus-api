namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Logging;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

using NSubstitute;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;

public sealed class ServiceNameEnricherTests
{
    private readonly ILogEventPropertyFactory _propertyFactory = Substitute.For<ILogEventPropertyFactory>();

    // ─── CA-01 / CA-02: enricher emite ServiceName em cada log event ──────

    [Fact]
    public void Enrich_DadoNomeCanonicoSelecao_DeveAdicionarPropriedadeServiceName()
    {
        ServiceNameEnricher enricher = new(UniPlusServiceNames.Selecao);
        LogEvent evento = CriarEventoSimples();

        enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties[ServiceNameEnricher.PropertyName]).Value
            .Should().Be(UniPlusServiceNames.Selecao);
    }

    [Fact]
    public void Enrich_DadoNomeCanonicoIngresso_DeveAdicionarPropriedadeServiceName()
    {
        ServiceNameEnricher enricher = new(UniPlusServiceNames.Ingresso);
        LogEvent evento = CriarEventoSimples();

        enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties[ServiceNameEnricher.PropertyName]).Value
            .Should().Be(UniPlusServiceNames.Ingresso);
    }

    [Fact]
    public void Enrich_QuandoEventoJaTemServiceName_DeveSobrescreverComValorCanonico()
    {
        // AddOrUpdate garante que um valor de teste residual ou propagação errônea
        // de outro pipeline não sobreviva — o enricher é a fonte de verdade.
        const string valorAntigo = "valor-residual-de-outro-pipeline";
        ServiceNameEnricher enricher = new(UniPlusServiceNames.Selecao);
        LogEvent evento = CriarEventoComPropriedade(ServiceNameEnricher.PropertyName, valorAntigo);

        enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties[ServiceNameEnricher.PropertyName]).Value
            .Should().Be(UniPlusServiceNames.Selecao);
    }

    // ─── Guards e invariantes ──────────────────────────────────────────────

    [Fact]
    public void Constructor_DadoNomeVazio_DeveLancarArgumentException()
    {
        Action acao = () => _ = new ServiceNameEnricher(string.Empty);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_DadoNomeNulo_DeveLancarArgumentException()
    {
        Action acao = () => _ = new ServiceNameEnricher(null!);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_DadoNomeApenasWhitespace_DeveLancarArgumentException()
    {
        Action acao = () => _ = new ServiceNameEnricher("   ");

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enrich_DadoLogEventNulo_DeveLancarArgumentNullException()
    {
        ServiceNameEnricher enricher = new(UniPlusServiceNames.Selecao);

        Action acao = () => enricher.Enrich(null!, _propertyFactory);

        acao.Should().Throw<ArgumentNullException>();
    }

    // ─── CA-03 (fluxo integrado com SerilogConfiguration) ──────────────────

    [Fact]
    public void ConfigurarSerilog_DadoNomeServico_DeveEmitirLogEventComPropriedadeServiceName()
    {
        // Pipeline real Serilog com sink customizado de captura — garante que o
        // ServiceNameEnricher é registrado por ConfigurarSerilog quando nomeServico
        // é fornecido (componente 2 da ADR-0052).
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Enabled"] = "false" })
            .Build();

        CapturingSink sink = new();
        Logger logger = new LoggerConfiguration()
            .ConfigurarSerilog(configuration, UniPlusServiceNames.Selecao)
            .WriteTo.Sink(sink)
            .CreateLogger();

        try
        {
            logger.Information("evento de teste");
        }
        finally
        {
            logger.Dispose();
        }

        sink.Eventos.Should().ContainSingle();
        LogEvent emitido = sink.Eventos[0];
        emitido.Properties.Should().ContainKey(ServiceNameEnricher.PropertyName);
        ((ScalarValue)emitido.Properties[ServiceNameEnricher.PropertyName]).Value
            .Should().Be(UniPlusServiceNames.Selecao);
    }

    [Fact]
    public void ConfigurarSerilog_SemNomeServico_NaoDeveAdicionarPropriedadeServiceName()
    {
        // Quando nomeServico é null, ServiceNameEnricher não é registrado — evita
        // emitir property vazia/null para callers legados que ainda não rotulam logs.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Enabled"] = "false" })
            .Build();

        CapturingSink sink = new();
        Logger logger = new LoggerConfiguration()
            .ConfigurarSerilog(configuration)
            .WriteTo.Sink(sink)
            .CreateLogger();

        try
        {
            logger.Information("evento de teste");
        }
        finally
        {
            logger.Dispose();
        }

        sink.Eventos.Should().ContainSingle();
        sink.Eventos[0].Properties.Should().NotContainKey(ServiceNameEnricher.PropertyName);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static LogEvent CriarEventoSimples()
        => CriarEventoComPropriedade("Mensagem", "evento de teste");

    private static LogEvent CriarEventoComPropriedade(string nome, string valor)
    {
        MessageTemplate template = new MessageTemplateParser().Parse("template");
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            template,
            [new LogEventProperty(nome, new ScalarValue(valor))]);
    }

    /// <summary>
    /// Sink Serilog que captura eventos em memória para assertions — equivalente ao
    /// pattern de FakeLogger usado em outros testes do middleware Wolverine.
    /// </summary>
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            Eventos.Add(logEvent);
        }
    }
}
