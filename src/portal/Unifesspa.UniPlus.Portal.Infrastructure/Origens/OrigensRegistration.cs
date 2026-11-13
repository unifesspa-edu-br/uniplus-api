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
    /// Registra as origens e a composição da página pública do certame.
    /// </summary>
    /// <remarks>
    /// A configuração é obrigatória e validada na subida: a página pública é a razão de ser da
    /// Portal, e sem os endereços das origens ela não tem como responder. Faltando, o processo não
    /// sobe — em vez de subir pronto e responder erro a cada leitura de candidato.
    /// </remarks>
    public static IServiceCollection AddPortalOrigens(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegistrarOpcoes(services, configuration);
        RegistrarClienteDaOrigem<ISelecaoCertamePublicoApi>(services, PoliticaDeSelecao, static opcoes => opcoes.SelecaoBaseUrl);
        RegistrarClienteDaOrigem<IPublicacoesAtosApi>(services, PoliticaDePublicacoes, static opcoes => opcoes.PublicacoesBaseUrl);
        RegistrarComposicaoDaPagina(services);

        return services;
    }

    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OrigensOptions>()
            .Bind(configuration.GetSection(OrigensOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<OrigensOptions>, OrigensOptionsValidator>());
    }

    /// <summary>
    /// O cliente é gerado em tempo de compilação: se a interface sair do que o gerador sabe montar, a falha vem na
    /// compilação, e não na primeira chamada. Cada origem tem a política de resiliência própria, para que o circuito
    /// aberto por uma não bloqueie a outra.
    /// </summary>
    private static void RegistrarClienteDaOrigem<TApi>(
        IServiceCollection services, string politica, Func<OrigensOptions, string> endereco)
        where TApi : class
    {
        services.AddRefitGeneratedClient<TApi>()
            .ConfigureHttpClient((provedor, cliente) => ConfigurarCliente(provedor, cliente, endereco))
            .ConfigurePrimaryHttpMessageHandler(SemCredencialNemRedirecionamento)
            .RedactLoggedHeaders(CabecalhosOcultos)
            .AddResilienceHandler(politica, ConfigurarResiliencia);
    }

    private static void RegistrarComposicaoDaPagina(IServiceCollection services)
    {
        services.AddScoped<IContratoPublicoDoCertame, ContratoPublicoDoCertameHttp>();
        services.AddScoped<IRegistroDeAtosDoCertame, RegistroDeAtosDoCertameHttp>();
        services.AddScoped<ComposicaoDaPaginaDoCertame>();
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
    /// se declarou indisponível. O relógio é o do contêiner: os testes o trocam para exercitar espera
    /// e expiração sem tempo real.
    /// </remarks>
    private static void ConfigurarResiliencia(
        ResiliencePipelineBuilder<HttpResponseMessage> construtor,
        ResilienceHandlerContext contexto)
    {
        OrigensOptions opcoes = contexto.ServiceProvider.GetRequiredService<IOptions<OrigensOptions>>().Value;

        construtor.TimeProvider = contexto.ServiceProvider.GetRequiredService<TimeProvider>();
        construtor.AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos));
        AdicionarRetentativa(construtor, opcoes);
        construtor
            .AddCircuitBreaker(CorteDeCircuito(opcoes))
            .AddTimeout(TimeSpan.FromSeconds(opcoes.TimeoutPorTentativaEmSegundos));
    }

    /// <summary>
    /// Zero retentativas desliga a estratégia: a biblioteca recusa montá-la com zero.
    /// </summary>
    private static void AdicionarRetentativa(ResiliencePipelineBuilder<HttpResponseMessage> construtor, OrigensOptions opcoes)
    {
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
    }

    private static HttpCircuitBreakerStrategyOptions CorteDeCircuito(OrigensOptions opcoes) =>
        new()
        {
            SamplingDuration = TimeSpan.FromSeconds(opcoes.JanelaDeAmostragemDoCorteEmSegundos),
            MinimumThroughput = opcoes.ChamadasMinimasParaAvaliarCorte,
            FailureRatio = opcoes.ProporcaoDeFalhaParaAbrirCorte,
            BreakDuration = TimeSpan.FromSeconds(opcoes.DuracaoDoCorteEmSegundos),
        };
}
