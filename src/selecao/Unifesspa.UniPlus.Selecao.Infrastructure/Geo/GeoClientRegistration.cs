namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

using Refit;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Registra o cliente do Geo (issue #1865): a consulta de conferência e o guardião do token M2M,
/// com as políticas que tornam a chamada resistente a instabilidade do Geo.
/// </summary>
/// <remarks>
/// Diferente do SIGAA (<c>AddSigaaVinculoDiscenteClient</c>, integração OPCIONAL — só registra se
/// <c>BaseUrl</c> estiver configurado), este registro é INCONDICIONAL: <c>ValidateOnStart()</c>
/// sem guarda nenhuma faz o processo não subir sem a config completa — a conferência do endereço
/// é invariante de elegibilidade (ADR-0136), nunca uma integração de melhor esforço.
/// </remarks>
public static class GeoClientRegistration
{
    private const string PoliticaDeConsulta = "geo-conferencia";

    public static IServiceCollection AddGeoClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GeoOptions>()
            .BindConfiguration(GeoOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        RegistrarClienteDeAutenticacao(services);
        RegistrarClienteDeConsulta(services);

        return services;
    }

    private static void RegistrarClienteDeAutenticacao(IServiceCollection services)
    {
        services.AddHttpClient(GeoTokenProvider.NomeDoClienteHttp, ConfigurarTimeoutDoClienteDeAutenticacao)
            .ConfigurePrimaryHttpMessageHandler(SemSeguirRedirecionamento)
            .RedactLoggedHeaders(["Authorization"])
            .AddStandardResilienceHandler()
            .Configure(ConfigurarResilienciaDaAutenticacao);

        services.TryAddSingleton<GeoTokenProvider>();
    }

    private static void RegistrarClienteDeConsulta(IServiceCollection services)
    {
        services.AddTransient<GeoAuthenticationHandler>();
        services.AddScoped<IConferenciaEnderecoGeo, ConferenciaEnderecoGeo>();

        // Cliente gerado em tempo de compilação (mesmo padrão de SigaaClientRegistration) — a
        // variante que monta a requisição por reflexão exige um pacote à parte e falha só na
        // primeira chamada, em vez de na compilação.
        IHttpClientBuilder cliente = services.AddRefitGeneratedClient<IGeoApi>()
            .ConfigureHttpClient(ConfigurarEndereco)
            .ConfigurePrimaryHttpMessageHandler(SemSeguirRedirecionamento)
            .RedactLoggedHeaders(["Authorization"]);

        // Resiliência por fora, autenticação por dentro: cada nova tentativa reentra na
        // autenticação e relê o token vigente (mesma ordem de SigaaClientRegistration).
        cliente.AddResilienceHandler(PoliticaDeConsulta, ConfigurarResilienciaDaConsulta);
        cliente.AddHttpMessageHandler<GeoAuthenticationHandler>();
    }

    private static System.Net.Http.HttpMessageHandler SemSeguirRedirecionamento() =>
        new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };

    private static void ConfigurarEndereco(IServiceProvider provedor, HttpClient cliente)
    {
        GeoOptions opcoes = provedor.GetRequiredService<IOptions<GeoOptions>>().Value;
        cliente.BaseAddress = new Uri(opcoes.BaseUrl, UriKind.Absolute);
        cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos * 2);
    }

    // Sem BaseAddress de propósito: GeoTokenProvider posta numa URI absoluta, e o endpoint OAuth é
    // opcional na configuração — atribuí-lo aqui lançaria UriFormatException na criação do cliente.
    private static void ConfigurarTimeoutDoClienteDeAutenticacao(IServiceProvider provedor, HttpClient cliente)
    {
        GeoOptions opcoes = provedor.GetRequiredService<IOptions<GeoOptions>>().Value;
        cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos);
    }

    private static void ConfigurarResilienciaDaAutenticacao(HttpStandardResilienceOptions opcoes, IServiceProvider provedor)
    {
        GeoOptions geo = provedor.GetRequiredService<IOptions<GeoOptions>>().Value;

        opcoes.Retry.MaxRetryAttempts = 2;
        opcoes.AttemptTimeout.Timeout = TimeSpan.FromSeconds(geo.TimeoutPorTentativaEmSegundos);
        opcoes.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(geo.TimeoutTotalEmSegundos);
    }

    private static void ConfigurarResilienciaDaConsulta(
        ResiliencePipelineBuilder<HttpResponseMessage> construtor, ResilienceHandlerContext contexto)
    {
        GeoOptions geo = contexto.ServiceProvider.GetRequiredService<IOptions<GeoOptions>>().Value;
        construtor.TimeProvider = contexto.ServiceProvider.GetRequiredService<TimeProvider>();

        construtor
            .AddTimeout(TimeSpan.FromSeconds(geo.TimeoutTotalEmSegundos))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = geo.MaximoDeRetentativas,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromSeconds(geo.TimeoutPorTentativaEmSegundos));
    }
}
