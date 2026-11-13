namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Email;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.Postgresql.Transport;
using Wolverine.Runtime;

public sealed class RoteamentoDoEnvioDeEmailTests
{
    [Fact(DisplayName = "Sem teto configurado, o listener da fila de e-mail entrega até cinco mensagens ao mesmo tempo")]
    public void ConfigurarRouting_SemConfiguracao_ListenerComOPadrao()
    {
        ListenerDaFila(Configuracao(null)).MaxDegreeOfParallelism.Should().Be(5);
    }

    [Fact(DisplayName = "O teto configurado é o paralelismo efetivo do listener da fila de e-mail")]
    public void ConfigurarRouting_TetoConfigurado_AplicadoAoListener()
    {
        Endpoint listener = ListenerDaFila(Configuracao("3"));

        listener.IsListener.Should().BeTrue();
        listener.MaxDegreeOfParallelism.Should().Be(3);
    }

    [Theory(DisplayName = "Teto inválido impede a subida antes de chegar ao Wolverine")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("21")]
    public void ConfigurarRouting_TetoInvalido_Falha(string teto)
    {
        Action configuracao = () => ListenerDaFila(Configuracao(teto));

        configuracao.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("EnviosSimultaneos");
    }

    private static Endpoint ListenerDaFila(IConfiguration configuracao)
    {
        WolverineOptions opts = new();
        EnvioDeEmailServiceCollectionExtensions.ConfigurarRoutingDeEmail(opts, configuracao);

        Endpoint listener = opts.Transports.GetOrCreate<PostgresqlTransport>().Endpoints()
            .Single(e => e.Uri == new Uri("postgresql://email_envio/"));

        // A configuração do listener é aplicada na compilação do endpoint, na subida do Wolverine; sem compilar, o teste
        // leria o padrão e não o efetivo.
        IWolverineRuntime runtime = Substitute.For<IWolverineRuntime>();
        runtime.Options.Returns(opts);
        listener.Compile(runtime);

        return listener;
    }

    private static IConfiguration Configuracao(string? teto) => new ConfigurationBuilder()
        .AddInMemoryCollection(teto is null ? [] : [new($"{EmailSmtpOptions.SectionName}:EnviosSimultaneos", teto)])
        .Build();
}
