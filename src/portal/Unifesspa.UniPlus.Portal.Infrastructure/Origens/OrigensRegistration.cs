namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

using Refit;

using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Registra o consumo das duas origens da página pública do certame — o contrato público de
/// Seleção e a consulta unificada de atos de Publicações — e a composição que depende delas.
/// </summary>
public static class OrigensRegistration
{
    private const string PoliticaDeSelecao = "portal-origem-selecao";
    private const string PoliticaDePublicacoes = "portal-origem-publicacoes";

    /// <summary>
    /// Cabeçalhos omitidos do log do cliente. As consultas não levam credencial, mas o registrador
    /// do cliente grava todos os cabeçalhos no nível mais detalhado; a omissão impede que uma
    /// credencial acrescentada por engano, ou um cookie devolvido pela origem, vá para o log.
    /// </summary>
    private static readonly string[] CabecalhosOcultos = ["Authorization", "Cookie", "Set-Cookie"];

    /// <summary>
    /// Registra as origens quando há endereço configurado.
    /// </summary>
    /// <remarks>
    /// Sem <c>Origens:SelecaoBaseUrl</c>, nada é registrado e a configuração não é validada — o
    /// mesmo tratamento que o repositório dá às demais integrações por rede: o processo sobe sem
    /// elas, o que mantém de pé as suítes que levantam a Portal sem as origens. A composição é
    /// registrada aqui, e não na camada de aplicação, porque não há como construí-la sem as
    /// origens; registrada incondicionalmente, a validação do contêiner derrubaria a subida.
    /// Havendo endereço, a configuração inteira é validada na subida.
    /// </remarks>
    public static IServiceCollection AddPortalOrigens(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection secao = configuration.GetSection(OrigensOptions.SectionName);

        if (string.IsNullOrWhiteSpace(secao[nameof(OrigensOptions.SelecaoBaseUrl)]))
        {
            return services;
        }

        services.AddOptions<OrigensOptions>()
            .Bind(secao)
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<OrigensOptions>, OrigensOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);

        // Cliente gerado em tempo de compilação: falha na compilação, e não na primeira chamada,
        // se a interface sair do que o gerador sabe montar.
        services.AddRefitGeneratedClient<ISelecaoCertamePublicoApi>()
            .ConfigureHttpClient((provedor, cliente) =>
                ConfigurarCliente(provedor, cliente, opcoes => opcoes.SelecaoBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(SemCredencialNemRedirecionamento)
            .RedactLoggedHeaders(CabecalhosOcultos)
            .AddResilienceHandler(PoliticaDeSelecao, ConfigurarResiliencia);

        services.AddRefitGeneratedClient<IPublicacoesAtosApi>()
            .ConfigureHttpClient((provedor, cliente) =>
                ConfigurarCliente(provedor, cliente, opcoes => opcoes.PublicacoesBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(SemCredencialNemRedirecionamento)
            .RedactLoggedHeaders(CabecalhosOcultos)
            .AddResilienceHandler(PoliticaDePublicacoes, ConfigurarResiliencia);

        services.AddScoped<IContratoPublicoDoCertame, ContratoPublicoDoCertameHttp>();
        services.AddScoped<IRegistroDeAtosDoCertame, RegistroDeAtosDoCertameHttp>();
        services.AddScoped<ComposicaoDaPaginaDoCertame>();

        return services;
    }

    /// <summary>
    /// Manipulador de rede sem cookies, sem credencial do processo e sem seguir redirecionamento.
    /// </summary>
    /// <remarks>
    /// Os endpoints consumidos são anônimos: nada que identifique o processo ou uma sessão deve
    /// sair daqui. Sem desligar os cookies, um cookie devolvido por uma origem seria reenviado nas
    /// requisições seguintes do mesmo manipulador, compartilhado entre leituras de candidatos
    /// diferentes. E o endereço das origens é configurado, não descoberto: um redirecionamento é
    /// resposta inesperada, não desvio a seguir.
    /// </remarks>
    internal static HttpClientHandler SemCredencialNemRedirecionamento() =>
        new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseDefaultCredentials = false,
        };

    private static void ConfigurarCliente(
        IServiceProvider provedor,
        HttpClient cliente,
        Func<OrigensOptions, string> endereco)
    {
        OrigensOptions opcoes = provedor.GetRequiredService<IOptions<OrigensOptions>>().Value;

        cliente.BaseAddress = new Uri(endereco(opcoes), UriKind.Absolute);

        // O limite do próprio cliente envolve a cadeia inteira, retentativas incluídas. Precisa de
        // folga sobre o limite total da política: igual ou menor, o cancelamento observado viria
        // daqui e o diagnóstico apontaria o cliente quando a causa é a política.
        cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos * 2);
    }

    /// <summary>
    /// Política das consultas na ordem canônica da biblioteca: limite total por fora, retentativa,
    /// corte de circuito dentro dela e limite por tentativa no miolo. Cada origem tem a sua
    /// instância, para que o circuito aberto por uma não bloqueie a outra.
    /// </summary>
    /// <remarks>
    /// O corte fica dentro da retentativa: aberto, ele faz a tentativa seguinte falhar na hora com
    /// um erro que a retentativa não repete, e o laço termina em vez de insistir numa origem que já
    /// se declarou indisponível.
    /// </remarks>
    private static void ConfigurarResiliencia(
        ResiliencePipelineBuilder<HttpResponseMessage> construtor,
        ResilienceHandlerContext contexto)
    {
        OrigensOptions opcoes = contexto.ServiceProvider.GetRequiredService<IOptions<OrigensOptions>>().Value;

        // Relógio do contêiner: os testes o trocam para exercitar espera e expiração sem tempo real.
        construtor.TimeProvider = contexto.ServiceProvider.GetRequiredService<TimeProvider>();

        construtor.AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos));

        // Zero retentativas desliga a estratégia: a biblioteca recusa montá-la com zero.
        if (opcoes.MaximoDeRetentativas > 0)
        {
            construtor.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = opcoes.MaximoDeRetentativas,
                Delay = TimeSpan.FromMilliseconds(opcoes.EsperaBaseEntreTentativasEmMilissegundos),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldRetryAfterHeader = true,
            });
        }

        construtor
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(opcoes.JanelaDeAmostragemDoCorteEmSegundos),
                MinimumThroughput = opcoes.ChamadasMinimasParaAvaliarCorte,
                FailureRatio = opcoes.ProporcaoDeFalhaParaAbrirCorte,
                BreakDuration = TimeSpan.FromSeconds(opcoes.DuracaoDoCorteEmSegundos),
            })
            .AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutPorTentativaEmSegundos));
    }
}
