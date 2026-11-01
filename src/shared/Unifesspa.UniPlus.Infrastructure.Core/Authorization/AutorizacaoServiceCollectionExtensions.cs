namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// Composição do ponto de decisão único de autorização (ADR-0078).
/// </summary>
public static class AutorizacaoServiceCollectionExtensions
{
    /// <summary>
    /// Registra o serviço de decisão, o agregador do sujeito e a verificação
    /// canônica de concessão.
    /// </summary>
    /// <remarks>
    /// O registro dos acessos (<see cref="IRegistroOperacionalRestrito"/>) é publicado no Kafka. Sem
    /// <c>RegistroDeAcesso:Kafka:BootstrapServers</c>, ou com opções inválidas, a subida falha, em vez de a aplicação
    /// entregar dado protegido sem registro. A subida valida a configuração e constrói o produtor; não verifica o
    /// broker nem o tópico.
    /// </remarks>
    public static IServiceCollection AddAutorizacao(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RegistroDeAcessoKafkaOptions>()
            .Bind(configuration.GetSection(RegistroDeAcessoKafkaOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RegistroDeAcessoKafkaOptions>, RegistroDeAcessoKafkaOptionsValidator>());
        services.TryAddSingleton<IRegistroOperacionalRestrito, RegistroDeAcessoKafka>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConferenciaDoRegistroDeAcessoNaSubida>());

        services.TryAddScoped<IAuthorizationSubjectResolver, HttpAuthorizationSubjectResolver>();

        // A verificação de concessão é o núcleo da decisão, não um item da lista
        // de verificações contextuais — entra pelo tipo concreto, e não como mais um
        // IPermissaoCheck, para que nenhum registro adicional a substitua.
        services.TryAddSingleton<GrantEfetivoAplicavelCheck>();

        // O impedimento por inscrição pergunta ao módulo dono das inscrições. Onde não há inscrição, a pergunta
        // tem sempre a mesma resposta; o módulo que as guarda substitui esta porta pela dele.
        services.TryAddSingleton<IInscricaoDoSujeitoNoProcesso, SemInscricaoNoProcesso>();
        services.TryAddScoped<ImpedimentoPorInscricaoCheck>();
        services.TryAddScoped<IAuthorizationDecisionService, AuthorizationDecisionService>();

        // A porta que os endpoints usam: uma pergunta, uma resposta.
        services.TryAddScoped<IVerificadorDeAcesso, VerificadorDeAcessoHttp>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IPermissoesEfetivas, PermissoesEfetivasHttp>();

        // O projetor decide cada item da resposta administrativa pela permissão do par (natureza, classificação)
        // e guarda as decisões na requisição, de onde a serialização defensiva as lê (ADR-0153).
        services.TryAddScoped<IDecisoesDaRequisicao, DecisoesDaRequisicaoHttp>();
        services.TryAddScoped<ProjetorDeItens>();

        // O módulo de autorização declara as próprias permissões no mesmo formato dos
        // demais; a conferência do conjunto roda na subida (ADR-0151).
        services.AddCatalogoDePermissoes<PermissaoAutorizacao>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConferenciaDoCatalogoNaSubida>());

        return services;
    }
}
