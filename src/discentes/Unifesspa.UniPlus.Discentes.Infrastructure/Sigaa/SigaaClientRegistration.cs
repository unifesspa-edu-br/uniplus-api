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
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

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

        return EstaConfigurado(configuration) ? RegistrarAcessoAoSigaa(services, configuration) : services;
    }

    private static bool EstaConfigurado(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.GetSection(SigaaOptions.SectionName)[nameof(SigaaOptions.BaseUrl)]);

    private static IServiceCollection RegistrarAcessoAoSigaa(IServiceCollection services, IConfiguration configuration)
    {
        RegistrarOpcoes(services, configuration);
        RegistrarClienteDeAutenticacao(services);
        RegistrarClienteDeConsulta(services);
        RegistrarSincronizacao(services, configuration);

        return services;
    }

    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SigaaOptions>()
            .Bind(configuration.GetSection(SigaaOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SigaaOptions>, SigaaOptionsValidator>());
    }

    /// <summary>
    /// O endereço de autenticação tem cliente próprio, sem o carimbo de token — carimbar
    /// a própria autenticação seria circular — e com política de resiliência separada da
    /// consulta. Compartilhar a política faria a interrupção por falhas da consulta
    /// bloquear também a renovação do token, e nada mais a liberaria.
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

    /// <summary>
    /// Usa o cliente gerado em tempo de compilação: a variante que monta a requisição por reflexão exige um pacote à
    /// parte e resolve em tempo de execução o que o gerador já resolveu na compilação, falhando só na primeira chamada.
    /// O cabeçalho de autorização é omitido do log: o registrador do cliente HTTP grava todos os cabeçalhos no nível
    /// mais detalhado, e sem a omissão o token sai inteiro para o log assim que alguém aumentar o detalhamento.
    /// </summary>
    /// <remarks>
    /// A ordem das duas últimas chamadas é a decisão de desenho mais importante deste registro, e é por isso que elas
    /// não estão encadeadas: a cadeia de envio é montada de fora para dentro na ordem em que os manipuladores são
    /// adicionados. Resiliência primeiro, portanto por fora; autenticação depois, por dentro. Assim cada nova tentativa
    /// reentra na autenticação e relê o token vigente, em vez de reenviar N vezes um cabeçalho carimbado uma única vez
    /// com um token vencido.
    /// </remarks>
    private static void RegistrarClienteDeConsulta(IServiceCollection services)
    {
        services.AddTransient<SigaaAuthenticationHandler>();
        services.AddScoped<ISigaaVinculoDiscenteClient, SigaaVinculoDiscenteClient>();

        IHttpClientBuilder cliente = services.AddRefitGeneratedClient<ISigaaVinculoDiscenteApi>()
            .ConfigureHttpClient(ConfigurarEndereco)
            .ConfigurePrimaryHttpMessageHandler(SemSeguirRedirecionamento)
            .RedactLoggedHeaders(["Authorization"]);

        cliente.AddResilienceHandler(PoliticaDeConsulta, ConfigurarResilienciaDaConsulta);
        cliente.AddHttpMessageHandler<SigaaAuthenticationHandler>();
    }

    /// <summary>
    /// Registra a reconciliação diária, que só faz sentido havendo origem de onde ler.
    /// </summary>
    /// <remarks>
    /// Fica aqui dentro, e não no registro geral do módulo, porque depende do cliente:
    /// registrada incondicionalmente, ela seria um serviço impossível de construir sempre
    /// que a origem não estivesse configurada — e a validação do contêiner derrubaria a
    /// subida do processo inteiro, inclusive das suítes que levantam a aplicação sem rede
    /// institucional.
    /// </remarks>
    private static void RegistrarSincronizacao(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SincronizacaoOptions>()
            .Bind(configuration.GetSection(SincronizacaoOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SincronizacaoOptions>, SincronizacaoOptionsValidator>());

        services.AddScoped<IGravadorDeVinculos, GravadorDeVinculos>();
        services.AddScoped<IRegistroDeExecucoes, RegistroDeExecucoes>();
        services.AddScoped<OrquestradorDeSincronizacao>();
    }

    /// <summary>
    /// O tempo limite do próprio cliente envolve a cadeia inteira, retentativas incluídas. Precisa de folga sobre o
    /// limite total da política: se fosse igual ou menor, o cancelamento observado viria daqui e o diagnóstico
    /// apontaria o cliente quando a causa é a política.
    /// </summary>
    private static void ConfigurarEndereco(IServiceProvider provedor, HttpClient cliente)
    {
        SigaaOptions opcoes = provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        cliente.BaseAddress = opcoes.BaseUrl;
        cliente.Timeout = opcoes.Resiliencia.TimeoutTotal * 2;
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
        ResilienciaDoSigaa resiliencia = provedor.GetRequiredService<IOptions<SigaaOptions>>().Value.Resiliencia;

        opcoes.Retry.MaxRetryAttempts = 2;
        opcoes.AttemptTimeout.Timeout = resiliencia.TimeoutPorTentativa;
        opcoes.TotalRequestTimeout.Timeout = resiliencia.TimeoutTotal;
        opcoes.CircuitBreaker.SamplingDuration = resiliencia.JanelaDeAmostragemDaInterrupcao;
        opcoes.CircuitBreaker.MinimumThroughput = resiliencia.ChamadasMinimasParaAvaliarInterrupcao;
    }

    /// <summary>
    /// Monta a política das consultas na ordem canônica da biblioteca: limite total por
    /// fora, retentativa em seguida, interrupção por falhas dentro dela e limite por tentativa
    /// no miolo.
    /// </summary>
    /// <remarks>
    /// A interrupção por falhas fica <b>dentro</b> da retentativa de propósito. Assim, quando
    /// ela abre, a tentativa seguinte falha na hora com um erro que a retentativa não
    /// considera repetível — e o laço termina, em vez de continuar batendo numa origem
    /// que já se declarou indisponível. Do lado de fora, ela só contaria desfechos finais
    /// e demoraria muito mais para reagir.
    ///
    /// A retentativa já respeita, por padrão da biblioteca, o intervalo que a origem pede
    /// quando responde que está sobrecarregada — não há nada a acrescentar para isso, só
    /// a cautela de não desligar. O relógio vem do contêiner: os testes trocam por um relógio controlado para
    /// exercitar espera e expiração sem depender do tempo real.
    /// </remarks>
    private static void ConfigurarResilienciaDaConsulta(
        ResiliencePipelineBuilder<HttpResponseMessage> construtor,
        ResilienceHandlerContext contexto)
    {
        ResilienciaDoSigaa resiliencia = LerOpcoes(contexto).Resiliencia;

        construtor.TimeProvider = contexto.ServiceProvider.GetRequiredService<TimeProvider>();
        construtor
            .AddTimeout(resiliencia.TimeoutTotal)
            .AddRetry(Retentativa(resiliencia))
            .AddCircuitBreaker(InterrupcaoPorFalhas(resiliencia))
            .AddTimeout(resiliencia.TimeoutPorTentativa);
    }

    private static HttpRetryStrategyOptions Retentativa(ResilienciaDoSigaa resiliencia) =>
        new()
        {
            MaxRetryAttempts = resiliencia.MaximoDeRetentativas,
            Delay = resiliencia.EsperaBaseEntreTentativas,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = true,
        };

    private static HttpCircuitBreakerStrategyOptions InterrupcaoPorFalhas(ResilienciaDoSigaa resiliencia) =>
        new()
        {
            SamplingDuration = resiliencia.JanelaDeAmostragemDaInterrupcao,
            MinimumThroughput = resiliencia.ChamadasMinimasParaAvaliarInterrupcao,
            FailureRatio = resiliencia.ProporcaoDeFalhaParaInterromper,
            BreakDuration = resiliencia.DuracaoDaInterrupcao,
        };

    private static SigaaOptions LerOpcoes(ResilienceHandlerContext contexto) =>
        contexto.ServiceProvider.GetRequiredService<IOptions<SigaaOptions>>().Value;
}
