namespace Unifesspa.UniPlus.Infrastructure.Core.ReverseProxy;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using IPNetwork = System.Net.IPNetwork;

/// <summary>
/// Extension methods that teach the application to read the original request scheme from
/// the proxy that terminates TLS in front of it.
/// </summary>
public static class ReverseProxyConfiguration
{
    // O Kestrel roda atrás de um proxy que termina o TLS, então o scheme que a aplicação
    // enxerga é sempre http — e é ele que compõe toda URL absoluta que a API emite: o Link
    // de navegação por cursor e o Location de cada 201. Sem traduzir X-Forwarded-Proto, o
    // contrato promete endereços que voltam sem TLS.
    //
    // Só XForwardedProto é processado, e a omissão dos outros dois é deliberada:
    //
    // - XForwardedHost faria um Host forjado compor as URLs que a API emite. Quem recebesse
    //   um Link nosso seria mandado para o endereço que o atacante escolheu, com o cursor
    //   cifrado junto.
    // - XForwardedFor reescreve RemoteIpAddress, que alimenta log e correlação. Passar a
    //   confiar nele é decisão própria, com consequências próprias — não carona nesta.
    private const ForwardedHeaders HeadersHonrados = ForwardedHeaders.XForwardedProto;

    /// <summary>
    /// Binds <see cref="ReverseProxyOptions"/> and configures forwarded header processing.
    /// Outside Development, startup fails if no trusted network is configured.
    /// </summary>
    public static IServiceCollection AddReverseProxyConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .Validate(
                options => environment.IsDevelopment() || options.TrustedNetworks.Count > 0,
                "ReverseProxy TrustedNetworks must be configured outside Development. Set 'ReverseProxy:TrustedNetworks' with the CIDR of the network the proxy reaches the API from; without it every forwarded header is ignored and absolute URLs are built with the wrong scheme.")
            .Validate(
                options => options.TrustedNetworks.All(rede => TentarConverter(rede, out _)),
                "ReverseProxy TrustedNetworks accepts CIDR notation only (e.g., '10.42.0.0/16').")
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ReverseProxyOptions>>(
                (forwardedOptions, ourOptionsAccessor) =>
                    ConfigurarForwardedHeaders(forwardedOptions, ourOptionsAccessor.Value));

        return services;
    }

    /// <summary>
    /// Applies forwarded header processing to the application pipeline. Must run before any
    /// middleware that reads the request scheme.
    /// </summary>
    public static IApplicationBuilder UseReverseProxyConfiguration(this IApplicationBuilder app) =>
        app.UseForwardedHeaders();

    private static void ConfigurarForwardedHeaders(
        ForwardedHeadersOptions forwardedOptions,
        ReverseProxyOptions options)
    {
        forwardedOptions.ForwardedHeaders = HeadersHonrados;

        // O default do framework confia em loopback. Aqui a lista é inteiramente declarada:
        // o que não está configurado não fala pelo cliente, e a configuração ausente resulta
        // em nenhuma confiança — nunca em confiança implícita.
        forwardedOptions.KnownIPNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();

        foreach (string rede in options.TrustedNetworks)
        {
            if (TentarConverter(rede, out IPNetwork convertida))
            {
                forwardedOptions.KnownIPNetworks.Add(convertida);
            }
        }
    }

    private static bool TentarConverter(string cidr, out IPNetwork rede) =>
        IPNetwork.TryParse(cidr, out rede);
}
