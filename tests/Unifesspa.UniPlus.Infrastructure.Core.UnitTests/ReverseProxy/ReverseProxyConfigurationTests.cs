namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.ReverseProxy;

using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.ReverseProxy;

using IPNetwork = System.Net.IPNetwork;

public sealed class ReverseProxyConfigurationTests
{
    private const string RedeConfiavel = "10.42.0.0/16";

    [Fact]
    public async Task Scheme_QuandoOHeaderVemDeForaDaRedeConfiavel_NaoEHonrado()
    {
        // O caminho que importa: sem esta recusa, qualquer cliente que alcance a aplicação
        // decide o scheme das URLs que ela emite só mandando o header.
        HttpContext context = await ProcessAsync(
            remoteIp: "203.0.113.9",
            forwardedProto: "https");

        context.Request.Scheme.Should().Be("http",
            because: "X-Forwarded-Proto vindo de fora das redes declaradas é ignorado");
    }

    [Fact]
    public async Task Scheme_QuandoOHeaderVemDaRedeConfiavel_EHonrado()
    {
        HttpContext context = await ProcessAsync(
            remoteIp: "10.42.0.7",
            forwardedProto: "https");

        context.Request.Scheme.Should().Be("https",
            because: "é o scheme com que a requisição chegou ao proxy que termina o TLS");
    }

    [Fact]
    public async Task Scheme_QuandoNaoHaHeaderEncaminhado_PermaneceODaConexao()
    {
        HttpContext context = await ProcessAsync(
            remoteIp: "10.42.0.7",
            forwardedProto: null);

        context.Request.Scheme.Should().Be("http");
    }

    [Fact]
    public void Options_QuandoConfiguradas_HonramSomenteOProtocolo()
    {
        ForwardedHeadersOptions options = ResolveForwardedHeaders(RedeConfiavel);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedProto,
            because: "XForwardedHost deixaria um Host forjado compor as URLs que a API emite, " +
                     "e XForwardedFor reescreve o IP que alimenta log e correlação");
    }

    [Fact]
    public void Options_QuandoConfiguradas_NaoHerdamConfiancaImplicita()
    {
        ForwardedHeadersOptions options = ResolveForwardedHeaders(RedeConfiavel);

        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().ContainSingle()
            .Which.Should().Be(IPNetwork.Parse(RedeConfiavel),
                because: "a lista é inteiramente declarada — o default do framework não se soma a ela");
    }

    [Fact]
    public void Startup_ForaDeDevelopment_SemRedeConfiavel_Falha()
    {
        Action resolver = () => ResolveOptions(Environments.Production);

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*TrustedNetworks*",
                because: "sem a lista todo header encaminhado é ignorado e as URLs saem com o scheme errado — " +
                         "falha silenciosa que a aplicação não deve aceitar subir");
    }

    [Fact]
    public void Startup_EmDevelopment_SemRedeConfiavel_Sobe()
    {
        Action resolver = () => ResolveOptions(Environments.Development);

        resolver.Should().NotThrow(
            because: "em desenvolvimento não há proxy na frente, e nenhum header encaminhado é honrado");
    }

    [Fact]
    public void Startup_ComRedeEmNotacaoInvalida_Falha()
    {
        Action resolver = () => ResolveOptions(Environments.Production, "10.42.0.0");

        resolver.Should().Throw<OptionsValidationException>()
            .WithMessage("*CIDR*");
    }

    [Fact]
    public async Task Scheme_QuandoOProxyChegaComoIPv4MapeadoEmIPv6_EHonrado()
    {
        // Socket dual-mode entrega o peer IPv4 como ::ffff:10.42.0.7. Se a comparação com a
        // rede declarada fosse por família de endereço, a rede IPv4 não casaria e a correção
        // seria inócua justamente onde ela precisa valer.
        HttpContext context = await ProcessAsync(
            remoteIp: "::ffff:10.42.0.7",
            forwardedProto: "https");

        context.Request.Scheme.Should().Be("https");
    }

    [Fact]
    public void ForwardedHeaders_ComRedeEmNotacaoInvalida_FalhaAntesDeCompor()
    {
        // A composição das ForwardedHeadersOptions converte cada entrada sem filtrar, e é
        // esta validação que a autoriza a fazê-lo: compor passa por ReverseProxyOptions.Value,
        // que valida antes de devolver.
        Action compor = () => ResolveForwardedHeaders("10.42.0.0");

        compor.Should().Throw<OptionsValidationException>()
            .WithMessage("*CIDR*");
    }

    private static async Task<HttpContext> ProcessAsync(string remoteIp, string? forwardedProto)
    {
        IOptions<ForwardedHeadersOptions> options =
            Options.Create(ResolveForwardedHeaders(RedeConfiavel));

        DefaultHttpContext context = new();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (forwardedProto is not null)
        {
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        }

        ForwardedHeadersMiddleware middleware = new(
            next: _ => Task.CompletedTask,
            loggerFactory: NullLoggerFactory.Instance,
            options: options);

        await middleware.Invoke(context);

        return context;
    }

    private static ForwardedHeadersOptions ResolveForwardedHeaders(params string[] trustedNetworks)
    {
        ServiceProvider provider = BuildProvider(Environments.Production, trustedNetworks);
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    private static void ResolveOptions(string environmentName, params string[] trustedNetworks)
    {
        ServiceProvider provider = BuildProvider(environmentName, trustedNetworks);
        _ = provider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
    }

    private static ServiceProvider BuildProvider(string environmentName, params string[] trustedNetworks)
    {
        Dictionary<string, string?> valores = [];
        for (int i = 0; i < trustedNetworks.Length; i++)
        {
            valores[$"{ReverseProxyOptions.SectionName}:TrustedNetworks:{i}"] = trustedNetworks[i];
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new ServiceCollection()
            .AddReverseProxyConfiguration(configuration, environment)
            .BuildServiceProvider();
    }
}
