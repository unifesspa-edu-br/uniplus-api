namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authentication;

using System.Security.Claims;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

public sealed class OidcAuthenticationConfigurationTests
{
    [Fact]
    public void AddOidcAuthentication_Should_RejectMissingAuthority_WithDataAnnotationMessage_NotNullReferenceException()
    {
        using ServiceProvider provider = BuildProvider(Environments.Production, new Dictionary<string, string?>());

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        OptionsValidationException exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Failures.Should()
            .Contain(failure => failure.Contains("Authority", StringComparison.OrdinalIgnoreCase),
                "the operator needs to know which configuration key is missing");
    }

    [Fact]
    public void AddOidcAuthentication_Should_RejectHttpAuthority_OutsideDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://insecure.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
                ["Auth:ClientId"] = "uniplus-api",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        OptionsValidationException exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Failures.Should()
            .Contain(failure => failure.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddOidcAuthentication_Should_AcceptHttpsAuthority_OutsideDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://idp.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
                ["Auth:ClientId"] = "uniplus-api",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOidcAuthentication_Should_AcceptHttpAuthority_InDevelopment()
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8080/realms/test",
                ["Auth:Audience"] = "uniplus",
                ["Auth:ClientId"] = "uniplus-api",
            });

        IOptions<AuthOptions> options = provider.GetRequiredService<IOptions<AuthOptions>>();
        Action act = () => _ = options.Value;

        act.Should().NotThrow();
    }

    [Theory(DisplayName = "Política Candidato identifica o titular pelo CPF, sem exigir papel")]
    [InlineData("cpf", "12345678909", null, true)]
    [InlineData("cpf", "12345678909", "uniplus-admin", true)]
    [InlineData("cpf", " ", "candidato", false)]
    [InlineData("sub", "abc", "candidato", false)]
    public async Task PoliticaCandidato_IdentificaPeloCpf_SemPapel(string claim, string valor, string? papel, bool esperado)
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://idp.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
                ["Auth:ClientId"] = "uniplus-api",
            });
        List<Claim> claims = [new(claim, valor)];
        if (papel is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, papel));
        }

        ClaimsPrincipal usuario = new(new ClaimsIdentity(claims, "teste"));

        AuthorizationResult resultado = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(usuario, "Candidato");

        resultado.Succeeded.Should().Be(esperado);
    }

    [Theory(DisplayName = "Rota sem atributo de autorização exige pessoa autenticada; requisição sem rota segue para o 404 ou 405")]
    [InlineData(false, Requisicao.ParaRota, false)]
    [InlineData(true, Requisicao.ParaRota, true)]
    [InlineData(false, Requisicao.SemRota, true)]
    [InlineData(false, Requisicao.ForaDoPipeline, false)]
    public async Task PoliticaPadrao_ExigePessoaAutenticadaNaRota(bool autenticada, Requisicao requisicao, bool esperado)
    {
        using ServiceProvider provider = BuildProvider(
            Environments.Production,
            new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://idp.example.com/realms/test",
                ["Auth:Audience"] = "uniplus",
                ["Auth:ClientId"] = "uniplus-api",
            });
        ClaimsPrincipal usuario = new(autenticada ? new ClaimsIdentity([new Claim("sub", "abc")], "teste") : new ClaimsIdentity());
        DefaultHttpContext? contexto = requisicao == Requisicao.ForaDoPipeline ? null : new DefaultHttpContext();
        if (requisicao == Requisicao.ParaRota)
        {
            contexto!.SetEndpoint(new RouteEndpoint(
                static _ => Task.CompletedTask, RoutePatternFactory.Parse("/api/rota"), 0, EndpointMetadataCollection.Empty, "rota"));
        }

        AuthorizationPolicy? padrao = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        padrao.Should().NotBeNull("sem política padrão, endpoint sem atributo fica público");
        AuthorizationResult resultado = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(usuario, contexto, padrao!);

        resultado.Succeeded.Should().Be(esperado);
    }

    public enum Requisicao
    {
        ParaRota,
        SemRota,
        ForaDoPipeline,
    }

    private static ServiceProvider BuildProvider(string environmentName, IDictionary<string, string?> configValues)
    {
        ServiceCollection services = new();
        services.AddLogging();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        services.AddOidcAuthentication(configuration, environment);

        return services.BuildServiceProvider();
    }
}
