namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

using Refit;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

/// <summary>
/// Registra o acesso à API do SIGAA: o cliente de consulta de vínculos, o guardião do
/// token e as políticas que tornam a chamada resistente a instabilidade da origem.
/// </summary>
public static class SigaaClientRegistration
{
    /// <summary>
    /// Nome da política de resiliência aplicada às consultas de vínculos.
    /// </summary>
    private const string PoliticaDeConsulta = "sigaa-vinculos";

    /// <summary>
    /// Registra o acesso ao SIGAA quando há endereço configurado.
    /// </summary>
    /// <remarks>
    /// Sem <c>Sigaa:BaseUrl</c>, nada é registrado e a configuração não é validada. É o
    /// mesmo tratamento que o projeto dá a outras integrações externas: o processo sobe
    /// sem a integração — o que mantém de pé o desenvolvimento local e as suítes que
    /// levantam a aplicação sem acesso à rede institucional — e quem tentar usar o
    /// cliente falha na hora de obtê-lo, com mensagem clara, em vez de descobrir tarde
    /// que a sincronização nunca teve como funcionar.
    /// </remarks>
    public static IServiceCollection AddSigaaVinculoDiscenteClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection secao = configuration.GetSection(SigaaOptions.SectionName);

        if (string.IsNullOrWhiteSpace(secao[nameof(SigaaOptions.BaseUrl)]))
        {
            return services;
        }

        services.AddOptions<SigaaOptions>()
            .Bind(secao)
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SigaaOptions>, SigaaOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);

        RegistrarClienteDeAutenticacao(services);
        RegistrarClienteDeConsulta(services);

        return services;
    }

    /// <summary>
    /// O endereço de autenticação tem cliente próprio, sem o carimbo de token — carimbar
    /// a própria autenticação seria circular — e com política de resiliência separada da
    /// consulta. Compartilhar a política faria o circuito aberto por instabilidade da
    /// consulta bloquear também a renovação do token, e nada mais destravaria.
    /// </summary>
    /// <remarks>
    /// O guardião do token é registrado como instância única: é ele que mantém o token
    /// entre requisições, e uma instância por requisição significaria autenticar de novo
    /// a cada chamada.
    /// </remarks>
    private static void RegistrarClienteDeAutenticacao(IServiceCollection services)
    {
        services.AddHttpClient(SigaaTokenProvider.NomeDoClienteHttp, ConfigurarEndereco)
            .ConfigurePrimaryHttpMessageHandler(SemSeguirRedirecionamento)
            .RedactLoggedHeaders(["Authorization"])
            .AddStandardResilienceHandler()
            .Configure(ConfigurarResilienciaDaAutenticacao);

        services.TryAddSingleton<SigaaTokenProvider>();
    }

    /// <summary>
    /// Cria o manipulador de rede que não segue redirecionamento.
    /// </summary>
    /// <remarks>
    /// Nem a autenticação nem a consulta de vínculos têm motivo legítimo para serem
    /// redirecionadas: o endereço da origem é configurado, não descoberto. Seguir
    /// redirecionamento aqui só abriria caminho para as credenciais e os dados pessoais
    /// saírem para um destino que ninguém configurou — em resposta que preserva método e
    /// corpo, o corpo do pedido de autenticação, com usuário e senha, iria junto para o
    /// novo destino. Um redirecionamento passa a ser resposta inesperada, tratada como
    /// falha, em vez de desvio silencioso.
    /// </remarks>
    private static HttpMessageHandler SemSeguirRedirecionamento() =>
        new HttpClientHandler { AllowAutoRedirect = false };

    private static void RegistrarClienteDeConsulta(IServiceCollection services)
    {
        services.AddTransient<SigaaAuthenticationHandler>();
        services.AddScoped<ISigaaVinculoDiscenteClient, SigaaVinculoDiscenteClient>();

        // Usa o cliente gerado em tempo de compilação. A variante que monta a requisição
        // por reflexão exige um pacote à parte e resolve em tempo de execução o que o
        // gerador já resolveu na compilação — inclusive falhando só quando a primeira
        // chamada acontece, em vez de na compilação.
        IHttpClientBuilder cliente = services.AddRefitGeneratedClient<ISigaaVinculoDiscenteApi>()
            .ConfigureHttpClient(ConfigurarEndereco)
            .ConfigurePrimaryHttpMessageHandler(SemSeguirRedirecionamento)

            // O cabeçalho de autorização é omitido do log. O registrador do próprio
            // cliente HTTP grava todos os cabeçalhos no nível mais detalhado e não
            // esconde nada por conta própria — sem esta linha, o token sai inteiro para o
            // log assim que alguém aumentar o detalhamento para investigar.
            .RedactLoggedHeaders(["Authorization"]);

        // A ordem destas duas chamadas é a decisão de desenho mais importante deste
        // registro, e é por isso que elas não estão encadeadas: a cadeia de envio é
        // montada de fora para dentro na ordem em que os manipuladores são adicionados.
        // Resiliência primeiro, portanto por fora; autenticação depois, por dentro. Assim
        // cada nova tentativa reentra na autenticação e relê o token vigente, em vez de
        // reenviar N vezes um cabeçalho carimbado uma única vez com um token vencido.
        cliente.AddResilienceHandler(PoliticaDeConsulta, ConfigurarResilienciaDaConsulta);
        cliente.AddHttpMessageHandler<SigaaAuthenticationHandler>();
    }

    private static void ConfigurarEndereco(IServiceProvider provedor, HttpClient cliente)
    {
        SigaaOptions opcoes = provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        cliente.BaseAddress = new Uri(opcoes.BaseUrl, UriKind.Absolute);

        // O tempo limite do próprio cliente envolve a cadeia inteira, retentativas
        // incluídas. Precisa de folga sobre o limite total da política: se fosse igual ou
        // menor, o cancelamento observado viria daqui e o diagnóstico apontaria o cliente
        // quando a causa é a política.
        cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutTotalEmSegundos * 2);
    }

    /// <summary>
    /// Autenticação tolera instabilidade breve, mas não insiste: credencial recusada não
    /// melhora com repetição, e cada tentativa extra atrasa toda requisição que espera
    /// pelo token.
    /// </summary>
    private static void ConfigurarResilienciaDaAutenticacao(
        HttpStandardResilienceOptions opcoes,
        IServiceProvider provedor)
    {
        SigaaOptions sigaa = provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        opcoes.Retry.MaxRetryAttempts = 2;
        opcoes.AttemptTimeout.Timeout = TimeSpan.FromSeconds(sigaa.TimeoutPorTentativaEmSegundos);
        opcoes.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(sigaa.TimeoutTotalEmSegundos);
        opcoes.CircuitBreaker.SamplingDuration =
            TimeSpan.FromSeconds(sigaa.JanelaDeAmostragemDoCorteEmSegundos);
        opcoes.CircuitBreaker.MinimumThroughput = sigaa.ChamadasMinimasParaAvaliarCorte;
    }

    /// <summary>
    /// Monta a política das consultas na ordem canônica da biblioteca: limite total por
    /// fora, retentativa em seguida, corte de circuito dentro dela e limite por tentativa
    /// no miolo.
    /// </summary>
    /// <remarks>
    /// O corte de circuito fica <b>dentro</b> da retentativa de propósito. Assim, quando
    /// ele abre, a tentativa seguinte falha na hora com um erro que a retentativa não
    /// considera repetível — e o laço termina, em vez de continuar batendo numa origem
    /// que já se declarou indisponível. Do lado de fora, ele só contaria desfechos finais
    /// e demoraria muito mais para reagir.
    ///
    /// A retentativa já respeita, por padrão da biblioteca, o intervalo que a origem pede
    /// quando responde que está sobrecarregada — não há nada a acrescentar para isso, só
    /// a cautela de não desligar.
    /// </remarks>
    private static void ConfigurarResilienciaDaConsulta(
        ResiliencePipelineBuilder<HttpResponseMessage> construtor,
        ResilienceHandlerContext contexto)
    {
        SigaaOptions sigaa = LerOpcoes(contexto);

        // Relógio vindo do contêiner: os testes trocam por um relógio controlado para
        // exercitar espera e expiração sem depender do tempo real.
        construtor.TimeProvider = contexto.ServiceProvider.GetRequiredService<TimeProvider>();

        construtor
            .AddTimeout(TimeSpan.FromSeconds(sigaa.TimeoutTotalEmSegundos))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = sigaa.MaximoDeRetentativas,
                Delay = TimeSpan.FromMilliseconds(sigaa.EsperaBaseEntreTentativasEmMilissegundos),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldRetryAfterHeader = true,
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(sigaa.JanelaDeAmostragemDoCorteEmSegundos),
                MinimumThroughput = sigaa.ChamadasMinimasParaAvaliarCorte,
                FailureRatio = sigaa.ProporcaoDeFalhaParaAbrirCorte,
                BreakDuration = TimeSpan.FromSeconds(sigaa.DuracaoDoCorteEmSegundos),
            })
            .AddTimeout(TimeSpan.FromSeconds(sigaa.TimeoutPorTentativaEmSegundos));
    }

    private static SigaaOptions LerOpcoes(ResilienceHandlerContext contexto) =>
        contexto.ServiceProvider.GetRequiredService<IOptions<SigaaOptions>>().Value;
}
