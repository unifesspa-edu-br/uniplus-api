namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Hosting;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConferenciaDosEnumsDeContratoNaSubidaTests
{
    private static readonly Assembly ExecutavelValido = typeof(ConferenciaDosEnumsDeContratoNaSubida).Assembly;

    [Theory(DisplayName = "Enums defeituosos impedem o início do host, com início sequencial e concorrente de serviços")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_EnumsDefeituosos_FalhaComOsDoisDefeitosESemIniciarServico(bool iniciaServicosEmParalelo)
    {
        SondaDeInicioDoHost sonda = new();
        using IHost host = ConstruirHost(ExecutavelComEnumsDefeituosos.Criar(), sonda, iniciaServicosEmParalelo);

        Func<Task> iniciar = () => host.StartAsync();

        // O host entrega a exceção do catálogo direta, sem AggregateException, nos dois modos.
        CodigosDeContratoInvalidosException falha = (await iniciar.Should().ThrowExactlyAsync<CodigosDeContratoInvalidosException>()).Which;

        falha.Defeitos.Should().HaveCount(2)
            .And.Contain(static d => d.StartsWith($"{ExecutavelComEnumsDefeituosos.EnumComCodigoRepetido}.", StringComparison.Ordinal))
            .And.Contain(static d => d.StartsWith($"{ExecutavelComEnumsDefeituosos.EnumComCodigoAusente}.", StringComparison.Ordinal));
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

    [Fact(DisplayName = "Registrar a conferência duas vezes registra uma só")]
    public void AddConferenciaDosEnumsDeContrato_ChamadaDuasVezes_RegistraUmaSo()
    {
        ServiceCollection services = new();

        services.AddConferenciaDosEnumsDeContrato(ExecutavelValido).AddConferenciaDosEnumsDeContrato(ExecutavelValido);

        using ServiceProvider provedor = services.BuildServiceProvider();
        provedor.GetServices<IHostedService>().OfType<ConferenciaDosEnumsDeContratoNaSubida>().Should().ContainSingle();
    }

    [Fact(DisplayName = "O executável nulo é recusado")]
    public void AddConferenciaDosEnumsDeContrato_ExecutavelNulo_Recusa() =>
        FluentActions.Invoking(static () => new ServiceCollection().AddConferenciaDosEnumsDeContrato(null!))
            .Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "A coleção de serviços nula é recusada")]
    public void AddConferenciaDosEnumsDeContrato_ColecaoNula_Recusa() =>
        FluentActions.Invoking(static () => ConferenciaDosEnumsDeContratoServiceCollectionExtensions.AddConferenciaDosEnumsDeContrato(null!, ExecutavelValido))
            .Should().Throw<ArgumentNullException>();

    // A sonda é registrada primeiro: no início sequencial ela iniciaria antes da conferência se esta falhasse em StartAsync.
    private static IHost ConstruirHost(Assembly executavel, SondaDeInicioDoHost sonda, bool iniciaServicosEmParalelo) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(opcoes => opcoes.ServicesStartConcurrently = iniciaServicosEmParalelo);
                services.AddSingleton<IHostedService>(sonda);
                services.AddConferenciaDosEnumsDeContrato(executavel);
            })
            .Build();
}
