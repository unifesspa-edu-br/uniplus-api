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
    private const string TrustedNetwork = "10.42.0.0/16";

    [Fact]
    public async Task UseReverseProxyConfiguration_HeaderDeForaDaTrustedNetwork_NaoEHonrado()
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
    public async Task UseReverseProxyConfiguration_HeaderDaTrustedNetwork_EHonrado()
    {
        HttpContext context = await ProcessAsync(
            remoteIp: "10.42.0.7",
            forwardedProto: "https");

        context.Request.Scheme.Should().Be("https",
            because: "é o scheme com que a requisição chegou ao proxy que termina o TLS");
    }

    [Fact]
    public async Task UseReverseProxyConfiguration_SemHeaderEncaminhado_MantemOSchemeDaConexao()
    {
        HttpContext context = await ProcessAsync(
            remoteIp: "10.42.0.7",
            forwardedProto: null);

        context.Request.Scheme.Should().Be("http");
    }

    [Fact]
    public void AddReverseProxyConfiguration_QuandoConfigurada_HonraSomenteOProtocolo()
    {
        ForwardedHeadersOptions options = ResolveForwardedHeaders(TrustedNetwork);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedProto,
            because: "XForwardedHost deixaria um Host forjado compor as URLs que a API emite, " +
                     "e XForwardedFor reescreve o IP que alimenta log e correlação");
    }

    [Fact]
    public void AddReverseProxyConfiguration_QuandoConfigurada_NaoHerdaConfiancaImplicita()
    {
        ForwardedHeadersOptions options = ResolveForwardedHeaders(TrustedNetwork);

        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().ContainSingle()
            .Which.Should().Be(IPNetwork.Parse(TrustedNetwork),
                because: "a lista é inteiramente declarada — o default do framework não se soma a ela");
    }

    [Fact]
    public void AddReverseProxyConfiguration_ForaDeDevelopmentSemTrustedNetwork_Falha()
    {
        Action resolve = () => ResolveOptions(Environments.Production);

        resolve.Should().Throw<OptionsValidationException>()
            .WithMessage("*TrustedNetworks*",
                because: "sem a lista todo header encaminhado é ignorado e as URLs saem com o scheme errado — " +
                         "falha silenciosa que a aplicação não deve aceitar subir");
    }

    [Fact]
    public void AddReverseProxyConfiguration_EmDevelopmentSemTrustedNetwork_Sobe()
    {
        Action resolve = () => ResolveOptions(Environments.Development);

        resolve.Should().NotThrow(
            because: "em desenvolvimento não há proxy na frente, e nenhum header encaminhado é honrado");
    }

    [Fact]
    public void AddReverseProxyConfiguration_ComRedeEmNotacaoInvalida_FalhaNaValidacao()
    {
        Action resolve = () => ResolveOptions(Environments.Production, "10.42.0.0");

        resolve.Should().Throw<OptionsValidationException>()
            .WithMessage("*CIDR*");
    }

    [Fact]
    public async Task UseReverseProxyConfiguration_ProxyComoIPv4MapeadoEmIPv6_EHonrado()
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
    public void AddReverseProxyConfiguration_ComRedeEmNotacaoInvalida_FalhaAntesDeComporOsHeaders()
    {
        // A composição das ForwardedHeadersOptions converte cada entrada sem filtrar, e é
        // esta validação que a autoriza a fazê-lo: compor passa por ReverseProxyOptions.Value,
        // que valida antes de devolver.
        Action compose = () => ResolveForwardedHeaders("10.42.0.0");

        compose.Should().Throw<OptionsValidationException>()
            .WithMessage("*CIDR*");
    }

    /// <summary>
    /// Em Development sem rede declarada — o único ambiente que chega aqui sem lista, porque
    /// fora dele a validação derruba o boot — o processamento fica DESLIGADO.
    /// </summary>
    /// <remarks>
    /// As duas coleções vazias não significam "não confie em ninguém" para o middleware:
    /// significam "não filtre por endereço", e ele passa a honrar o header de qualquer origem.
    /// Sem este desligamento, qualquer cliente que alcançasse a aplicação em desenvolvimento
    /// decidiria o scheme das URLs que ela emite.
    /// </remarks>
    [Fact]
    public async Task AddReverseProxyConfiguration_EmDevelopmentSemTrustedNetwork_NaoHonraOHeaderDeNinguem()
    {
        ForwardedHeadersOptions options = ResolveForwardedHeadersFor(Environments.Development);

        HttpContext context = await ProcessWithOptionsAsync(
            options,
            remoteIp: "203.0.113.9",
            forwardedProto: "https");

        context.Request.Scheme.Should().Be("http",
            because: "lista vazia é negação explícita, não ausência de filtro");
    }

    private static async Task<HttpContext> ProcessAsync(string remoteIp, string? forwardedProto)
    {
        IOptions<ForwardedHeadersOptions> options =
            Options.Create(ResolveForwardedHeaders(TrustedNetwork));

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

    private static async Task<HttpContext> ProcessWithOptionsAsync(
        ForwardedHeadersOptions options,
        string remoteIp,
        string? forwardedProto)
    {
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
            options: Options.Create(options));

        await middleware.Invoke(context);

        return context;
    }

    private static ForwardedHeadersOptions ResolveForwardedHeadersFor(
        string environmentName,
        params string[] trustedNetworks)
    {
        ServiceProvider provider = BuildProvider(environmentName, trustedNetworks);
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
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
        Dictionary<string, string?> values = [];
        for (int i = 0; i < trustedNetworks.Length; i++)
        {
            values[$"{ReverseProxyOptions.SectionName}:TrustedNetworks:{i}"] = trustedNetworks[i];
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new ServiceCollection()
            .AddReverseProxyConfiguration(configuration, environment)
            .BuildServiceProvider();
    }
}
