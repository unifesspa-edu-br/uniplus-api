namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Logging;

using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Sentinelas do <see cref="SerilogConfiguration"/> — travam o contrato de
/// formato do Console sink com o <c>derivedFields</c> do datasource Loki no
/// Grafana (PR <c>uniplus-infra#225</c>). Refactor que altere a posição ou o
/// formato dos placeholders <c>TraceId</c>/<c>SpanId</c> falha aqui antes de
/// quebrar silenciosamente o drill-down log↔trace em produção.
/// </summary>
public sealed class SerilogConfigurationTests
{
    /// <summary>Regex exata configurada no datasource Loki via PR <c>uniplus-infra#225</c>.</summary>
    private const string DerivedFieldsRegex = @"(?:traceID|trace_id|TraceId)[""]?[=:]\s*[""]?([a-fA-F0-9]{32})";

    // ─── CA-01: sentinela do outputTemplate ────────────────────────────────

    [Fact]
    public void ConsoleOutputTemplate_DeveConterPlaceholdersTraceIdESpanId()
    {
        SerilogConfiguration.ConsoleOutputTemplate.Should().Contain("{TraceId}",
            "regex do derivedFields exige `TraceId=<hex32>` no body");
        SerilogConfiguration.ConsoleOutputTemplate.Should().Contain("{SpanId}",
            "rastreabilidade de span individual no log line");
    }

    [Fact]
    public void ConsoleOutputTemplate_DeveTerSeparadorIgualAposTraceId()
    {
        // A regex captura o valor APÓS `=` (ou `:`). Validamos o caractere
        // separador para que refactor que mude `TraceId=` para `TraceId:` em
        // teste mantenha a invariante coberta — mesma regra para SpanId.
        SerilogConfiguration.ConsoleOutputTemplate.Should().MatchRegex(
            @"TraceId\s*[=:]\s*\{TraceId\}",
            "derivedFields regex busca por TraceId seguido de = ou : antes do hex");
        SerilogConfiguration.ConsoleOutputTemplate.Should().MatchRegex(
            @"SpanId\s*[=:]\s*\{SpanId\}");
    }

    // ─── CA-02/CA-04: pipeline real emite log que casa a regex ─────────────

    [Fact]
    public void ConfigurarSerilog_ComActivityAtiva_DeveEmitirLogLineCasandoDerivedFieldsRegex()
    {
        // Pipeline real Serilog + Activity ativa + sink que captura a STRING
        // formatada (não a property). Garante que a regex do derivedFields
        // realmente casa com o body do log line entregue ao stdout.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Enabled"] = "false" })
            .Build();

        using ActivitySource source = new(nameof(ConfigurarSerilog_ComActivityAtiva_DeveEmitirLogLineCasandoDerivedFieldsRegex));
        using ActivityListener listener = new()
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        CapturingTextSink sink = new(SerilogConfiguration.ConsoleOutputTemplate);
        Logger logger = new LoggerConfiguration()
            .ConfigurarSerilog(configuration, UniPlusServiceNames.Selecao)
            .WriteTo.Sink(sink)
            .CreateLogger();

        string traceIdEsperado;
        try
        {
            using Activity? activity = source.StartActivity("smoke");
            activity.Should().NotBeNull();
            traceIdEsperado = activity!.TraceId.ToString();
            logger.Information("requisição processada");
        }
        finally
        {
            logger.Dispose();
        }

        sink.Linhas.Should().ContainSingle();
        string linha = sink.Linhas[0];
        Match match = Regex.Match(linha, DerivedFieldsRegex);
        match.Success.Should().BeTrue(
            $"linha gerada `{linha}` deve casar a regex do derivedFields do Loki");
        match.Groups[1].Value.Should().Be(traceIdEsperado,
            "captura da regex deve ser exatamente o TraceId do Activity ativo");
    }

    [Fact]
    public void ConfigurarSerilog_SemActivity_NaoDeveEmitirLinhaCasandoDerivedFieldsRegex()
    {
        // Garante o caso oposto: sem trace ativo, a linha emitida não casa a
        // regex — derivedFields não vai renderizar link spurious para um trace
        // que não existe.
        Activity.Current = null;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Enabled"] = "false" })
            .Build();

        CapturingTextSink sink = new(SerilogConfiguration.ConsoleOutputTemplate);
        Logger logger = new LoggerConfiguration()
            .ConfigurarSerilog(configuration, UniPlusServiceNames.Selecao)
            .WriteTo.Sink(sink)
            .CreateLogger();

        try
        {
            logger.Information("startup pre-routing");
        }
        finally
        {
            logger.Dispose();
        }

        sink.Linhas.Should().ContainSingle();
        Regex.IsMatch(sink.Linhas[0], DerivedFieldsRegex)
            .Should().BeFalse("string vazia após TraceId= não deve casar 32 hex chars");
    }

    /// <summary>
    /// Sink Serilog que renderiza cada evento usando o mesmo
    /// <see cref="MessageTemplateTextFormatter"/> do <c>WriteTo.Console</c>,
    /// capturando a STRING entregue ao stdout para assertion textual.
    /// </summary>
    private sealed class CapturingTextSink(string outputTemplate) : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter = new(outputTemplate, formatProvider: null);

        public List<string> Linhas { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            using StringWriter writer = new();
            _formatter.Format(logEvent, writer);
            Linhas.Add(writer.ToString().TrimEnd('\r', '\n'));
        }
    }
}
