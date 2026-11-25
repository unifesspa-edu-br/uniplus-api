namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.EnumsDefeituososDeTeste;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Hosting;

public sealed class ConferenciaDosEnumsDeContratoNaSubidaTests
{
    private static readonly Assembly ExecutavelValido = typeof(VerificadorOptions).Assembly;

    [Theory(DisplayName = "Enums defeituosos impedem o início do host, com início sequencial e concorrente de serviços")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_EnumsDefeituosos_FalhaComOsDoisDefeitosESemIniciarServico(bool iniciaServicosEmParalelo)
    {
        SondaDeInicioDoHost sonda = new();
        using IHost host = ConstruirHost(typeof(ComCodigoRepetidoNoExecutavel).Assembly, sonda, iniciaServicosEmParalelo);

        Func<Task> iniciar = () => host.StartAsync();

        // O host entrega a exceção do catálogo direta, sem AggregateException, nos dois modos.
        CodigosDeContratoInvalidosException falha = (await iniciar.Should().ThrowExactlyAsync<CodigosDeContratoInvalidosException>()).Which;

        falha.Defeitos.Should().HaveCount(2)
            .And.Contain(static d => d.StartsWith($"{nameof(ComCodigoRepetidoNoExecutavel)}.", StringComparison.Ordinal))
            .And.Contain(static d => d.StartsWith($"{nameof(ComCodigoAusenteNoExecutavel)}.", StringComparison.Ordinal));
        sonda.ChamadasDeStartAsync.Should().Be(0, "nenhum StartAsync roda depois de um StartingAsync que falha");
    }

    [Theory(DisplayName = "Executável sem defeito não impede o início: a sonda inicia")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_ExecutavelSemDefeito_IniciaOsServicos(bool iniciaServicosEmParalelo)
    {
        SondaDeInicioDoHost sonda = new();
        using IHost host = ConstruirHost(ExecutavelValido, sonda, iniciaServicosEmParalelo);

        await host.StartAsync();
        await host.StopAsync();

        sonda.ChamadasDeStartAsync.Should().Be(1);
    }

    [Fact(DisplayName = "O executável nulo é recusado")]
    public void Construtor_ExecutavelNulo_Recusa() =>
        FluentActions.Invoking(static () => new ConferenciaDosEnumsDeContratoNaSubida(null!)).Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "A composição completa do verificador registra uma só conferência dos enums de contrato")]
    public void ComposicaoCompleta_RegistraUmaSoConferenciaDosEnums()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(PontoDeEntradaDoVerificador.ConfiguracaoMinima);

        host.Services.GetServices<IHostedService>().OfType<ConferenciaDosEnumsDeContratoNaSubida>().Should().ContainSingle();
    }

    [Fact(DisplayName = "A conferência registrada na composição completa do verificador não encontra defeito no catálogo real")]
    public async Task ComposicaoCompleta_ConferenciaRegistrada_NaoLancaNoCatalogoReal()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(PontoDeEntradaDoVerificador.ConfiguracaoMinima);
        ConferenciaDosEnumsDeContratoNaSubida conferencia = host.Services.GetServices<IHostedService>().OfType<ConferenciaDosEnumsDeContratoNaSubida>().Single();

        Func<Task> iniciando = () => conferencia.StartingAsync(CancellationToken.None);

        await iniciando.Should().NotThrowAsync();
    }

    // A sonda é registrada primeiro: no início sequencial ela iniciaria antes da conferência se esta falhasse em StartAsync.
    private static IHost ConstruirHost(Assembly executavel, SondaDeInicioDoHost sonda, bool iniciaServicosEmParalelo) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(opcoes => opcoes.ServicesStartConcurrently = iniciaServicosEmParalelo);
                services.AddSingleton<IHostedService>(sonda);
                services.AddHostedService(_ => new ConferenciaDosEnumsDeContratoNaSubida(executavel));
            })
            .Build();
}
