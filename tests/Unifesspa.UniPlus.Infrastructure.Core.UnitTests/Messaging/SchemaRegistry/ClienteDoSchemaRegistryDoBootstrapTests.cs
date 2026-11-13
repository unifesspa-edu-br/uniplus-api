namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>
/// A posse do cliente do Schema Registry criado antes de existir o <see cref="IServiceProvider"/>: o host a assume
/// como <see cref="IHostedService"/> e a descarta uma única vez no encerramento, inclusive o provider OAuth que o
/// <c>CachedSchemaRegistryClient</c> não dispõe sozinho.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ClienteDoSchemaRegistryDoBootstrapTests
{
    private static readonly Dictionary<string, string?> ConfiguracaoOAuth = new()
    {
        ["SchemaRegistry:Url"] = "http://schema-registry.invalid:8081",
        ["SchemaRegistry:AuthType"] = "OAuthBearer",
        ["SchemaRegistry:OAuth:TokenEndpoint"] = "https://keycloak.invalid/token",
        ["SchemaRegistry:OAuth:ClientId"] = "cliente-de-teste",
        ["SchemaRegistry:OAuth:ClientSecret"] = "segredo-de-teste",
    };

    private static SchemaRegistrySettings SettingsDe(IReadOnlyDictionary<string, string?> valores) =>
        Configuracao(valores).GetSection(SchemaRegistrySettings.SectionName).Get<SchemaRegistrySettings>()!;

    private static IConfiguration Configuracao(IReadOnlyDictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static SchemaRegistryDoBootstrap DonoRegistrado(IServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<SchemaRegistryDoBootstrap>().Single();

    [Fact(DisplayName = "O dono do bootstrap entra uma única vez no container como hosted service")]
    public void CreateClient_RegistraODonoUmaUnicaVezComoHostedService()
    {
        ServiceCollection services = new();
        SchemaRegistryServiceCollectionExtensions.CreateClient(
            SettingsDe(ConfiguracaoOAuth), NullLoggerFactory.Instance, services);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>().OfType<SchemaRegistryDoBootstrap>().Should().ContainSingle();
    }

    [Fact(DisplayName = "Encerrar o container dispõe o provider OAuth que o cliente do bootstrap usa")]
    public void EncerrarOContainer_DisponhaOProviderOAuthDoBootstrap()
    {
        ServiceCollection services = new();
        SchemaRegistryServiceCollectionExtensions.CreateClient(
            SettingsDe(ConfiguracaoOAuth), NullLoggerFactory.Instance, services);
        ServiceProvider provider = services.BuildServiceProvider();
        IAuthenticationHeaderValueProvider autenticacao = DonoRegistrado(provider).Autenticacao!;

        provider.Dispose();

        Action cabecalho = () => autenticacao.GetAuthenticationHeader();
        cabecalho.Should().Throw<ObjectDisposedException>(
            because: "o dono dispõe o provider ao ser descartado pelo container, uma vez que o cliente não o faz");
    }

    [Fact(DisplayName = "O provider OAuth do bootstrap é o registrado no container, sem segundo provider com cache próprio")]
    public void CreateClient_ComOAuth_RegistraOMesmoProviderQueOClienteUsa()
    {
        ServiceCollection services = new();
        SchemaRegistryServiceCollectionExtensions.CreateClient(
            SettingsDe(ConfiguracaoOAuth), NullLoggerFactory.Instance, services);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuthenticationHeaderValueProvider>()
            .Should().BeSameAs(DonoRegistrado(provider).Autenticacao);
    }

    [Fact(DisplayName = "Sem OAuth bearer o bootstrap não cria nem registra provider de autenticação")]
    public void CreateClient_SemOAuth_NaoRegistraProviderDeAutenticacao()
    {
        ServiceCollection services = new();
        SchemaRegistryServiceCollectionExtensions.CreateClient(
            SettingsDe(new Dictionary<string, string?> { ["SchemaRegistry:Url"] = "http://schema-registry.invalid:8081" }),
            NullLoggerFactory.Instance,
            services);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IAuthenticationHeaderValueProvider>().Should().BeNull();
        DonoRegistrado(provider).Autenticacao.Should().BeNull();
    }

    [Fact(DisplayName = "Sem URL configurada o bootstrap recusa criar o cliente")]
    public void CreateClient_SemUrl_Lanca()
    {
        Action criar = () => SchemaRegistryServiceCollectionExtensions.CreateClient(
            new SchemaRegistrySettings(), NullLoggerFactory.Instance, new ServiceCollection());

        criar.Should().Throw<InvalidOperationException>().WithMessage("*SchemaRegistry:Url*");
    }

    [Fact(DisplayName = "AddSchemaRegistry depois do bootstrap reaproveita o cliente e o provider, sem criar outros")]
    public void AddSchemaRegistry_AposOBootstrap_ReaproveitaClienteEProvider()
    {
        IConfiguration configuracao = Configuracao(ConfiguracaoOAuth);
        ServiceCollection services = new();
        services.AddLogging();
        ISchemaRegistryClient bootstrap = SchemaRegistryServiceCollectionExtensions.CreateClient(
            SettingsDe(ConfiguracaoOAuth), NullLoggerFactory.Instance, services);
        services.AddSingleton(bootstrap);
        services.AddSchemaRegistry(configuracao);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISchemaRegistryClient>().Should().BeSameAs(bootstrap);
        provider.GetRequiredService<IAuthenticationHeaderValueProvider>()
            .Should().BeSameAs(DonoRegistrado(provider).Autenticacao);
    }

    [Fact(DisplayName = "AddSchemaRegistry sem bootstrap cria o cliente e o provider OAuth pelo container")]
    public void AddSchemaRegistry_SemBootstrap_CriaClienteEProviderPeloContainer()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSchemaRegistry(Configuracao(ConfiguracaoOAuth));

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISchemaRegistryClient>().Should().NotBeNull();
        provider.GetRequiredService<IAuthenticationHeaderValueProvider>()
            .Should().BeOfType<OAuthBearerAuthenticationHeaderValueProvider>();
    }

    [Fact(DisplayName = "AddSchemaRegistry com URL vazia não registra o cliente")]
    public void AddSchemaRegistry_SemUrl_NaoRegistraOCliente()
    {
        ServiceCollection services = new();
        services.AddSchemaRegistry(Configuracao(new Dictionary<string, string?>()));

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ISchemaRegistryClient>().Should().BeNull();
    }

    [Fact(DisplayName = "AddSchemaRegistry recusa configuração inválida na hora do registro")]
    public void AddSchemaRegistry_ComConfiguracaoInvalida_Lanca()
    {
        ServiceCollection services = new();
        IConfiguration invalida = Configuracao(new Dictionary<string, string?>
        {
            ["SchemaRegistry:Url"] = "http://schema-registry.invalid:8081",
            ["SchemaRegistry:AuthType"] = "OAuthBearer",
        });

        Action registrar = () => services.AddSchemaRegistry(invalida);

        registrar.Should().Throw<InvalidOperationException>().WithMessage("*SchemaRegistry*inválida*");
    }
}
