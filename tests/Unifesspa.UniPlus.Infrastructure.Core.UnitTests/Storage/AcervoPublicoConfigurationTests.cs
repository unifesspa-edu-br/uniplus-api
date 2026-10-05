namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Storage;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

public sealed class AcervoPublicoConfigurationTests
{
    [Fact(DisplayName = "Fora de Development, sem o endereço do acervo a partida falha")]
    public void ForaDeDevelopment_SemEnderecoBase_Falha()
    {
        Action resolver = () => Resolver(Environments.Production, enderecoBase: null);

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*AcervoPublico:EnderecoBase must be configured*",
                because: "todo link de documento publicado é montado sobre ele, e um link quebrado em edital não avisa ninguém");
    }

    [Fact(DisplayName = "Fora de Development, o endereço do acervo em http é recusado na partida")]
    public void ForaDeDevelopment_EnderecoBaseHttp_Falha()
    {
        Action resolver = () => Resolver(Environments.Production, "http://uniplus.unifesspa.edu.br/acervo");

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*https*");
    }

    private static void Resolver(string ambiente, string? enderecoBase)
    {
        Dictionary<string, string?> config = new()
        {
            ["AcervoPublico:Bucket"] = "uniplus-acervo-publico",
            ["AcervoPublico:EnderecoBase"] = enderecoBase,
        };
        ServiceCollection services = new();
        services.AddUniPlusAcervoPublico(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = ambiente });

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<AcervoPublicoOptions>>().Value;
    }
}
