namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Selecao.API;

public sealed class ProvaPresencialOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Startup_LimiteNaoPositivo_Recusa(int limite)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Selecao:ProvaPresencial:LimiteDeMunicipios"] = limite.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddSingleton(TimeProvider.System);
        services.AddSelecaoModule(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        Action iniciar = () => provider.GetRequiredService<IStartupValidator>().Validate();
        iniciar.Should().Throw<OptionsValidationException>().WithMessage("*limite de municípios*");
    }
}
