namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Comportamento do fluxo dedicado onde as decisões de acesso são registradas:
/// grava o registro no seu próprio pipeline, mantém-no fora do pipeline comum da
/// aplicação e nunca deixa uma falha do destino alcançar quem decidiu.
/// </summary>
public sealed class SerilogRegistroOperacionalRestritoTests
{
    [Fact(DisplayName = "Grava a decisão no fluxo dedicado, com o motivo em valor canônico")]
    public void Registrar_DecisaoNegada_GravaNoFluxoDedicado()
    {
        SinkCapturador capturado = new();

        using (SerilogRegistroOperacionalRestrito registro = ComDestino(capturado))
        {
            registro.Registrar(RegistroNegado());
        }

        LogEvent evento = capturado.Eventos.Should().ContainSingle().Subject;
        string texto = evento.RenderMessage(CultureInfo.InvariantCulture);

        texto.Should().Contain("sem_concessao_aplicavel");
        texto.Should().NotContain("SemConcessaoAplicavel",
            "o registro é lido fora do processo — gravar o identificador C# quebraria o vocabulário canônico");
    }

    [Fact(DisplayName = "Desabilitado, não instala destino nem grava")]
    public void Registrar_Desabilitado_NaoGrava()
    {
        using SerilogRegistroOperacionalRestrito registro = Criar(habilitado: false);

        registro.Ativo.Should().BeFalse();
        registro.Invoking(alvo => alvo.Registrar(RegistroNegado())).Should().NotThrow();
    }

    [Fact(DisplayName = "Com a observabilidade desligada, o exportador não é instalado")]
    public void AddAutorizacao_SemObservabilidade_DesligaORegistro()
    {
        // Onde não há coletor (o compose do monólito desliga a observabilidade
        // justamente por isso), manter o exportador de pé só produz tentativas de
        // conexão contra um endereço que ninguém atende.
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [OpenTelemetryConfiguration.EnabledConfigurationKey] = "false",
            })
            .Build());
        services.AddAutorizacao(services.BuildServiceProvider().GetRequiredService<IConfiguration>());

        RegistroOperacionalRestritoOptions opcoes = services.BuildServiceProvider()
            .GetRequiredService<IOptions<RegistroOperacionalRestritoOptions>>().Value;

        opcoes.Habilitado.Should().BeFalse();
    }

    [Fact(DisplayName = "Com a observabilidade ligada, o registro permanece ativo")]
    public void AddAutorizacao_ComObservabilidade_MantemORegistro()
    {
        ServiceCollection services = [];
        IConfiguration configuracao = new ConfigurationBuilder().Build();
        services.AddSingleton(configuracao);
        services.AddAutorizacao(configuracao);

        services.BuildServiceProvider()
            .GetRequiredService<IOptions<RegistroOperacionalRestritoOptions>>()
            .Value.Habilitado.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha do destino não propaga para quem decidiu")]
    public void Registrar_SinkQueFalha_NaoPropaga()
    {
        using SerilogRegistroOperacionalRestrito registro = ComDestino(new SinkQueFalha());

        Action acao = () => registro.Registrar(RegistroNegado());

        acao.Should().NotThrow("quem registra não decide — uma falha do destino não muda o veredito");
    }

    private static SerilogRegistroOperacionalRestrito Criar(bool habilitado = true)
        => new(Options.Create(new RegistroOperacionalRestritoOptions { Habilitado = habilitado }));

    private static SerilogRegistroOperacionalRestrito ComDestino(ILogEventSink sink)
        => new(new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger());

    private static RegistroDecisaoAcesso RegistroNegado()
        => RegistroDecisaoAcesso.De(
            PermissionRequirement.From(
                "configuracao:motivos-decisao-recursal:manter",
                Sensibilidade.Interna).Value!,
            ResourceContext.From("MotivoDecisaoRecursal", Sensibilidade.Interna).Value!,
            AuthorizationRequestContext.From("req-1", DateTimeOffset.UnixEpoch, OrigemRequisicao.Api).Value!,
            AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

    private sealed class SinkCapturador : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }

    private sealed class SinkQueFalha : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => throw new InvalidOperationException("destino indisponível");
    }

}
