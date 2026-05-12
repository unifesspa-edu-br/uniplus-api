namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Logging;

using System.Diagnostics;

using AwesomeAssertions;

using NSubstitute;

using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using Unifesspa.UniPlus.Infrastructure.Core.Logging;

public sealed class TraceContextEnricherTests
{
    private readonly TraceContextEnricher _enricher = new();
    private readonly ILogEventPropertyFactory _propertyFactory = Substitute.For<ILogEventPropertyFactory>();

    // ─── CA-02: Activity ativa → TraceId/SpanId hex 32/16 chars no log event ───

    [Fact]
    public void Enrich_DadoActivityAtiva_DevePopularTraceIdComoHex32()
    {
        using ActivitySource source = new(nameof(Enrich_DadoActivityAtiva_DevePopularTraceIdComoHex32));
        using ActivityListener listener = CriarListenerAllData(source.Name);
        ActivitySource.AddActivityListener(listener);

        using Activity? activity = source.StartActivity("teste");
        activity.Should().NotBeNull("ActivityListener com AllData deve permitir criação");
        LogEvent evento = CriarEventoBasico();

        _enricher.Enrich(evento, _propertyFactory);

        string traceId = (string)((ScalarValue)evento.Properties[TraceContextEnricher.TraceIdPropertyName]).Value!;
        traceId.Should().MatchRegex("^[a-f0-9]{32}$", "Activity.TraceId.ToString() retorna hex32 lowercase");
        traceId.Should().Be(activity!.TraceId.ToString(), "valor emitido deve bater com TraceId do span ativo");
    }

    [Fact]
    public void Enrich_DadoActivityAtiva_DevePopularSpanIdComoHex16()
    {
        using ActivitySource source = new(nameof(Enrich_DadoActivityAtiva_DevePopularSpanIdComoHex16));
        using ActivityListener listener = CriarListenerAllData(source.Name);
        ActivitySource.AddActivityListener(listener);

        using Activity? activity = source.StartActivity("teste");
        activity.Should().NotBeNull();
        LogEvent evento = CriarEventoBasico();

        _enricher.Enrich(evento, _propertyFactory);

        string spanId = (string)((ScalarValue)evento.Properties[TraceContextEnricher.SpanIdPropertyName]).Value!;
        spanId.Should().MatchRegex("^[a-f0-9]{16}$", "Activity.SpanId.ToString() retorna hex16 lowercase");
        spanId.Should().Be(activity!.SpanId.ToString());
    }

    [Fact]
    public void Enrich_DadoActivityAtiva_RegexDoDerivedFieldsDeveCasarOTraceIdEmitido()
    {
        // Sentinela do contrato com uniplus-infra PR #225: a regex configurada no
        // datasource Loki (derivedFields) PRECISA casar com o valor emitido aqui.
        // Se este teste falhar, o link "Ver trace no Tempo" deixa de renderizar
        // mesmo com tudo "instalado". Defeito silencioso clássico que justifica
        // a existência da issue #227 (smoke E2E visual).
        using ActivitySource source = new(nameof(Enrich_DadoActivityAtiva_RegexDoDerivedFieldsDeveCasarOTraceIdEmitido));
        using ActivityListener listener = CriarListenerAllData(source.Name);
        ActivitySource.AddActivityListener(listener);
        using Activity? activity = source.StartActivity("teste");
        LogEvent evento = CriarEventoBasico();

        _enricher.Enrich(evento, _propertyFactory);

        string traceId = (string)((ScalarValue)evento.Properties[TraceContextEnricher.TraceIdPropertyName]).Value!;
        string linhaSimulada = $"[12:34:56 INF] mensagem TraceId={traceId} SpanId=...";
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(linhaSimulada, GrafanaDerivedFields.TraceIdMatcher);

        match.Success.Should().BeTrue("derivedFields do Loki deve capturar o TraceId no body do log");
        match.Groups[1].Value.Should().Be(traceId);
    }

    // ─── CA-03: Activity null → string vazia, sem casamento spurious ───────

    [Fact]
    public void Enrich_SemActivity_DeveEmitirTraceIdVazio()
    {
        // Listener anterior pode ter deixado Activity.Current setada por outro
        // teste no mesmo runner — explicitamente garantimos null aqui.
        Activity.Current = null;
        LogEvent evento = CriarEventoBasico();

        _enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties[TraceContextEnricher.TraceIdPropertyName]).Value
            .Should().Be(string.Empty);
        ((ScalarValue)evento.Properties[TraceContextEnricher.SpanIdPropertyName]).Value
            .Should().Be(string.Empty);
    }

    [Fact]
    public void Enrich_SemActivity_RegexDoDerivedFieldsNaoDeveCasarString_Vazia()
    {
        // Garantia de não-spurious: quando não há trace, o output `TraceId= SpanId=`
        // NÃO pode casar a regex hex32 — senão o Grafana renderiza link para um
        // trace inexistente, pior que não ter link nenhum.
        Activity.Current = null;
        LogEvent evento = CriarEventoBasico();

        _enricher.Enrich(evento, _propertyFactory);

        string traceId = (string)((ScalarValue)evento.Properties[TraceContextEnricher.TraceIdPropertyName]).Value!;
        string linhaSimulada = $"[12:34:56 INF] startup pre-routing TraceId={traceId} SpanId=";
        bool casa = System.Text.RegularExpressions.Regex.IsMatch(linhaSimulada, GrafanaDerivedFields.TraceIdMatcher);
        casa.Should().BeFalse("string vazia após TraceId= não deve casar 32 hex chars");
    }

    // ─── AddOrUpdate: re-execução sobrescreve valor anterior ───────────────

    [Fact]
    public void Enrich_QuandoEventoJaTemTraceId_DeveSobrescreverComValorDoActivityCorrente()
    {
        // Cenário hipotético: outro enricher upstream emitiu TraceId de fonte
        // diferente; o TraceContextEnricher é fonte de verdade do trace W3C ativo.
        using ActivitySource source = new(nameof(Enrich_QuandoEventoJaTemTraceId_DeveSobrescreverComValorDoActivityCorrente));
        using ActivityListener listener = CriarListenerAllData(source.Name);
        ActivitySource.AddActivityListener(listener);
        using Activity? activity = source.StartActivity("teste");
        LogEvent evento = CriarEventoComPropriedade(TraceContextEnricher.TraceIdPropertyName, "valor-residual-de-outro-pipeline");

        _enricher.Enrich(evento, _propertyFactory);

        ((ScalarValue)evento.Properties[TraceContextEnricher.TraceIdPropertyName]).Value
            .Should().Be(activity!.TraceId.ToString());
    }

    // ─── Guards ────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_LogEventNulo_DeveLancarArgumentNullException()
    {
        Action acao = () => _enricher.Enrich(null!, _propertyFactory);

        acao.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static LogEvent CriarEventoBasico()
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

    private static ActivityListener CriarListenerAllData(string sourceName) => new()
    {
        ShouldListenTo = s => s.Name == sourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
    };
}
