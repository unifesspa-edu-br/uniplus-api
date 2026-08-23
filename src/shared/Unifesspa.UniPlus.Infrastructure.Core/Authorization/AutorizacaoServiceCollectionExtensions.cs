namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Composição do ponto de decisão único de autorização (ADR-0078).
/// </summary>
public static class AutorizacaoServiceCollectionExtensions
{
    /// <summary>
    /// Registra o serviço de decisão, o agregador do sujeito, a verificação
    /// canônica de concessão e o registro operacional restrito.
    /// </summary>
    /// <remarks>
    /// Registro puro, sem trabalho de rede nem de disco: o destino do registro
    /// só abre arquivo na primeira escrita, de modo que subir a aplicação em um
    /// ambiente de teste não depende do destino existir.
    /// </remarks>
    public static IServiceCollection AddAutorizacao(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // O registro sai pelo mesmo protocolo da telemetria, então depende do
        // mesmo coletor: onde a observabilidade está desligada não há para onde
        // exportar, e manter o exportador de pé só produziria tentativas de
        // conexão contra um endereço que ninguém atende — que é justamente o
        // ruído que o desligamento existe para evitar.
        bool observabilidadeAtivada = configuration.GetValue(
            OpenTelemetryConfiguration.EnabledConfigurationKey,
            defaultValue: true);

        services.AddOptions<RegistroOperacionalRestritoOptions>()
            .Bind(configuration.GetSection(RegistroOperacionalRestritoOptions.SectionName))
            .PostConfigure(options => options.Habilitado &= observabilidadeAtivada)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IRegistroOperacionalRestrito, SerilogRegistroOperacionalRestrito>();
        services.TryAddScoped<IAuthorizationSubjectResolver, HttpAuthorizationSubjectResolver>();

        // A verificação de concessão é o núcleo da decisão, não um item da lista
        // declarativa (ADR-0080) — entra pelo tipo concreto, e não como mais um
        // IPermissaoCheck, para que nenhum registro adicional a substitua.
        services.TryAddSingleton<GrantEfetivoAplicavelCheck>();
        services.TryAddScoped<IAuthorizationDecisionService, AuthorizationDecisionService>();

        // A porta que os endpoints usam: uma pergunta, uma resposta.
        services.TryAddScoped<IVerificadorDeAcesso, VerificadorDeAcessoHttp>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
