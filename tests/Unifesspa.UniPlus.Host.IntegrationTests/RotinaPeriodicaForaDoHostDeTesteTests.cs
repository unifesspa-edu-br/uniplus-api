namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.API;

/// <summary>
/// A rotina periódica do Seleção é registrada pelo módulo, e o host de teste a remove mesmo com
/// Postgres e Wolverine ligados — uma varredura agindo sobre os dados que a suíte monta tornaria
/// os testes dependentes do relógio.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RotinaPeriodicaForaDoHostDeTesteTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public RotinaPeriodicaForaDoHostDeTesteTests(MonolitoPostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O módulo Seleção registra a rotina periódica, e o host de teste com Postgres a remove")]
    public void RotinaRegistradaPeloModulo_RemovidaDoHostDeTeste()
    {
        ServiceCollection modulo = new();
        modulo.AddSelecaoModule(new ConfigurationBuilder().Build());

        // Sem o registro, a remoção abaixo passaria sem provar nada.
        modulo.Should().Contain(
            d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType != null
                && d.ImplementationType.IsSubclassOf(typeof(RotinaPeriodicaHostedService)));

        _fixture.Factory.Services.GetServices<IHostedService>()
            .Should().NotContain(s => s is RotinaPeriodicaHostedService);
    }
}
