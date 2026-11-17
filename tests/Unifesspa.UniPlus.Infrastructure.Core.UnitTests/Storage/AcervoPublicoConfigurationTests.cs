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

    [Fact(DisplayName = "Fora de Development, sem o bucket do acervo a partida falha")]
    public void ForaDeDevelopment_SemBucket_Falha()
    {
        Action resolver = () => Resolver(Environments.Production, "https://uniplus.unifesspa.edu.br/acervo", bucket: null);

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*AcervoPublico:Bucket must be configured outside Development.*");
    }

    [Theory(DisplayName = "O endereço do acervo com query, fragmento ou sem esquema absoluto é recusado")]
    [InlineData("https://uniplus.unifesspa.edu.br/acervo?x=1")]
    [InlineData("https://uniplus.unifesspa.edu.br/acervo#topo")]
    [InlineData("/acervo")]
    public void EnderecoBaseInvalido_Falha(string enderecoBase)
    {
        Action resolver = () => Resolver(Environments.Production, enderecoBase);

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*AcervoPublico:EnderecoBase must be an absolute https URI without query or fragment (http is accepted only in Development).*");
    }

    [Fact(DisplayName = "Em Development, o endereço do acervo em http é aceito")]
    public void EmDevelopment_EnderecoBaseHttp_NaoFalha()
    {
        Action resolver = () => Resolver(Environments.Development, "http://localhost:9000/acervo");

        resolver.Should().NotThrow();
    }

    [Fact(DisplayName = "Em Development, sem bucket nem endereço a partida não falha")]
    public void EmDevelopment_SemBucketNemEndereco_NaoFalha()
    {
        Action resolver = () => Resolver(Environments.Development, enderecoBase: null, bucket: null);

        resolver.Should().NotThrow();
    }

    [Fact(DisplayName = "Fora de Development, o endereço do acervo em https é aceito")]
    public void ForaDeDevelopment_EnderecoBaseHttps_NaoFalha()
    {
        Action resolver = () => Resolver(Environments.Production, "https://uniplus.unifesspa.edu.br/acervo");

        resolver.Should().NotThrow();
    }

    private static void Resolver(string ambiente, string? enderecoBase, string? bucket = "uniplus-acervo-publico")
    {
        Dictionary<string, string?> configuracao = new()
        {
            ["AcervoPublico:Bucket"] = bucket,
            ["AcervoPublico:EnderecoBase"] = enderecoBase,
        };
        ServiceCollection services = new();
        services.AddUniPlusAcervoPublico(
            new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build(),
            new HostingEnvironment { EnvironmentName = ambiente });

        using ServiceProvider provedor = services.BuildServiceProvider();
        _ = provedor.GetRequiredService<IOptions<AcervoPublicoOptions>>().Value;
    }
}
