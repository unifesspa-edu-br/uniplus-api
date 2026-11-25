namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using JasperFx.CodeGeneration.Model;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Wolverine;

public sealed class ServiceLocationCodegenGuardTests
{
    [Fact]
    public async Task Varrer_FactoryScopedSemOptIn_IdentificaCommandSemExecutarHandler()
    {
        using IHost host = CriarHost(permitirServiceLocation: false);
        await host.StartAsync();

        ServiceLocationVarredura resultado = ServiceLocationCodegenGuard.Varrer(host.Services);

        resultado.Ofensores.Should().ContainSingle().Which.Should().Contain(nameof(CommandDeTeste));
        host.Services.GetRequiredService<EstadoDoHandler>().Executado.Should().BeFalse();
        await host.StopAsync();
    }

    [Fact]
    public async Task Varrer_FactoryScopedComOptIn_NaoIdentificaOfensores()
    {
        using IHost host = CriarHost(permitirServiceLocation: true);
        await host.StartAsync();

        ServiceLocationVarredura resultado = ServiceLocationCodegenGuard.Varrer(host.Services);

        resultado.Ofensores.Should().BeEmpty();
        resultado.Verificadas.Should().Be(1);
        await host.StopAsync();
    }

    private static IHost CriarHost(bool permitirServiceLocation) => Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton<EstadoDoHandler>();
            services.AddScoped<IDependenciaDeTeste>(sp => sp.GetRequiredService<EstadoDoHandler>());
        })
        .UseWolverine(opcoes => ConfigurarWolverine(opcoes, permitirServiceLocation))
        .Build();

    private static void ConfigurarWolverine(WolverineOptions opcoes, bool permitirServiceLocation)
    {
        opcoes.Discovery.DisableConventionalDiscovery();
        opcoes.Discovery.IncludeType(typeof(ExecutorDoCommandDeTeste));
        opcoes.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
        if (permitirServiceLocation)
        {
            opcoes.CodeGeneration.AlwaysUseServiceLocationFor<IDependenciaDeTeste>();
        }
    }

    public sealed record CommandDeTeste : ICommand<string>;

    public interface IDependenciaDeTeste
    {
        void RegistrarExecucao();
    }

    public sealed class EstadoDoHandler : IDependenciaDeTeste
    {
        public bool Executado { get; private set; }

        public void RegistrarExecucao() => Executado = true;
    }

    public static class ExecutorDoCommandDeTeste
    {
        public static string Handle(CommandDeTeste command, IDependenciaDeTeste dependencia)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(dependencia);
            dependencia.RegistrarExecucao();
            return "ok";
        }
    }
}
