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

        RegistrarRegistroDeAcesso(services, configuration);
        RegistrarDecisaoDeAcesso(services);
        RegistrarProjecaoPorProtecao(services);
        RegistrarCatalogoDePermissoes(services);

        return services;
    }

    /// <summary>
    /// O produtor do registro liga os logs do librdkafka ao <c>ILogger</c>, daí o <c>AddLogging</c>.
    /// </summary>
    private static void RegistrarRegistroDeAcesso(IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging();
        services.AddOptions<RegistroDeAcessoKafkaOptions>()
            .Bind(configuration.GetSection(RegistroDeAcessoKafkaOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RegistroDeAcessoKafkaOptions>, RegistroDeAcessoKafkaOptionsValidator>());
        services.TryAddSingleton<IRegistroOperacionalRestrito, RegistroDeAcessoKafka>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConferenciaDoRegistroDeAcessoNaSubida>());
    }

    /// <summary>
    /// A verificação de concessão é o núcleo da decisão, não um item da lista de verificações contextuais: entra pelo
    /// tipo concreto, e não como mais um <c>IPermissaoCheck</c>, para que nenhum registro adicional a substitua. O
    /// impedimento por inscrição pergunta ao módulo dono das inscrições; onde não há inscrição, a pergunta tem sempre
    /// a mesma resposta, e o módulo que as guarda substitui esta porta pela dele. A designação de banca segue o mesmo
    /// desenho, com uma verificação contextual que as permissões da banca declaram. A porta que os endpoints usam é uma
    /// pergunta, uma resposta.
    /// </summary>
    private static void RegistrarDecisaoDeAcesso(IServiceCollection services)
    {
        services.TryAddScoped<IAuthorizationSubjectResolver, HttpAuthorizationSubjectResolver>();
        services.TryAddSingleton<GrantEfetivoAplicavelCheck>();
        services.TryAddSingleton<IInscricaoDoSujeitoNoProcesso, SemInscricaoNoProcesso>();
        services.TryAddScoped<ImpedimentoPorInscricaoCheck>();
        services.TryAddSingleton<IDesignacaoDeBanca, SemDesignacaoDeBanca>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPermissaoCheck, DesignacaoNaBancaCheck>());
        services.TryAddScoped<IAuthorizationDecisionService, AuthorizationDecisionService>();
        services.TryAddScoped<IVerificadorDeAcesso, VerificadorDeAcessoHttp>();
        services.TryAddScoped<IPermissoesEfetivas, PermissoesEfetivasHttp>();
    }

    /// <summary>
    /// O projetor decide cada item da resposta administrativa pela permissão do par (natureza, classificação) e guarda
    /// as decisões na requisição, de onde a serialização defensiva as lê (ADR-0153). A defesa secundária é o
    /// serializador omitir o campo classificado sem decisão permitida na requisição.
    /// </summary>
    private static void RegistrarProjecaoPorProtecao(IServiceCollection services)
    {
        services.TryAddScoped<IDecisoesDaRequisicao, DecisoesDaRequisicaoHttp>();
        services.TryAddScoped<ProjetorDeItens>();
        services.AddOmissaoDeCampoClassificado();
    }

    /// <summary>
    /// O módulo de autorização declara as próprias permissões no mesmo formato dos demais; a conferência do conjunto
    /// roda na subida (ADR-0151).
    /// </summary>
    private static void RegistrarCatalogoDePermissoes(IServiceCollection services)
    {
        services.AddCatalogoDePermissoes<PermissaoAutorizacao>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConferenciaDoCatalogoNaSubida>());
    }
}
