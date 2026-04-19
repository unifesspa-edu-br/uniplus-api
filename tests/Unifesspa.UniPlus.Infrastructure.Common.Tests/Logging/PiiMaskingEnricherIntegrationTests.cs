namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.Logging;

using System.Collections.Concurrent;
using System.Globalization;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Common.Logging;

using ILogger = Microsoft.Extensions.Logging.ILogger;

public sealed partial class PiiMaskingEnricherIntegrationTests
{
    [Fact]
    public void LoggerMessage_DadoCpfComoParametroEstruturado_QuandoLogEmitido_EntaoSaiMascarado()
    {
        CapturingSink sink = new();
        using Serilog.Core.Logger serilog = new LoggerConfiguration()
            .Enrich.With<PiiMaskingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        using SerilogLoggerFactory factory = new(serilog);
        ILogger<PiiMaskingEnricherIntegrationTests> logger = factory.CreateLogger<PiiMaskingEnricherIntegrationTests>();

        LogInscricaoHomologada(logger, "123.456.789-01");

        LogEvent capturado = sink.Eventos.Should().ContainSingle().Which;

        ScalarValue cpfMascarado = (ScalarValue)capturado.Properties["CpfCandidato"];
        cpfMascarado.Value.Should().Be("***.***.***-01");

        string mensagemRenderizada = capturado.RenderMessage(CultureInfo.InvariantCulture);
        mensagemRenderizada.Should().Contain("***.***.***-01");
        mensagemRenderizada.Should().NotContain("123.456.789-01", "o CPF original não pode aparecer em texto claro");
        mensagemRenderizada.Should().NotContain("456.789", "nenhum dígito intermediário do CPF pode vazar");
    }

    [Fact]
    public void LoggerMessage_DadoCpfDentroDeObjetoEstruturado_QuandoLogEmitido_EntaoMascaraRecursivamente()
    {
        CapturingSink sink = new();
        using Serilog.Core.Logger serilog = new LoggerConfiguration()
            .Enrich.With<PiiMaskingEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        using SerilogLoggerFactory factory = new(serilog);
        ILogger<PiiMaskingEnricherIntegrationTests> logger = factory.CreateLogger<PiiMaskingEnricherIntegrationTests>();

        CandidatoDto candidato = new("João da Silva", "123.456.789-01");
        LogCandidatoHomologado(logger, candidato);

        LogEvent capturado = sink.Eventos.Should().ContainSingle().Which;

        StructureValue estrutura = (StructureValue)capturado.Properties["Candidato"];
        ScalarValue cpf = (ScalarValue)estrutura.Properties.First(p => p.Name == "Cpf").Value;
        cpf.Value.Should().Be("***.***.***-01");

        ScalarValue nome = (ScalarValue)estrutura.Properties.First(p => p.Name == "Nome").Value;
        nome.Value.Should().Be("João da Silva");

        capturado.RenderMessage(CultureInfo.InvariantCulture)
            .Should().NotContain("123.456.789-01");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inscrição homologada para candidato {CpfCandidato}")]
    private static partial void LogInscricaoHomologada(ILogger logger, string cpfCandidato);

    [LoggerMessage(Level = LogLevel.Information, Message = "Candidato homologado: {@Candidato}")]
    private static partial void LogCandidatoHomologado(ILogger logger, CandidatoDto candidato);

    private sealed record CandidatoDto(string Nome, string Cpf);

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Eventos { get; } = new();

        public void Emit(LogEvent logEvent) => Eventos.Enqueue(logEvent);
    }
}
